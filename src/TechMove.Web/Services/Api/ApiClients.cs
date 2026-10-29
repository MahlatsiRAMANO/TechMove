using System.Net.Http.Headers;
using System.Net.Http.Json;
using TechMove.Web.Models;
using TechMove.Web.ViewModels;

namespace TechMove.Web.Services.Api;

// ----------------------------------------------------------------------------------------------
// Frontend service layer: one typed HttpClient per API resource. Controllers depend on these
// interfaces only - the MVC app has no DbContext, no repositories and no business rules.
// ----------------------------------------------------------------------------------------------

public sealed record LoginResult(string Token, DateTime ExpiresAtUtc, string Username, string Role, int? ClientId);

public sealed record UserAccount(int Id, string Username, string Role, int? ClientId, string? ClientName, DateTime CreatedAtUtc);

public sealed record ExchangeRateResult(string Base, string Target, decimal Rate, DateTime RetrievedAtUtc);

public sealed record FileDownload(Stream Content, string FileName, string ContentType, IDisposable Owner) : IDisposable
{
    public void Dispose() => Owner.Dispose();
}

public interface IAuthApi
{
    /// <summary>Returns the token, or null when the username/password is wrong.</summary>
    Task<LoginResult?> LoginAsync(string username, string password, CancellationToken ct = default);

    /// <summary>Creates a Customer account and returns a token for it. Throws ApiValidationException (400/409) on bad input or a taken username.</summary>
    Task<LoginResult> RegisterAsync(string username, string password, CancellationToken ct = default);
}

public interface IUsersApi
{
    Task<IReadOnlyList<UserAccount>> GetAllAsync(CancellationToken ct = default);
    Task<UserAccount> UpdateAccessAsync(int id, string role, int? clientId, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public interface IClientsApi
{
    Task<IReadOnlyList<Client>> GetAllAsync(CancellationToken ct = default);
    Task<Client?> GetAsync(int id, CancellationToken ct = default);
    Task<Client> CreateAsync(Client client, CancellationToken ct = default);
    Task UpdateAsync(Client client, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public interface IContractsApi
{
    Task<IReadOnlyList<Contract>> SearchAsync(ContractSearchCriteria? criteria, CancellationToken ct = default);
    Task<IReadOnlyList<Contract>> GetOpenAsync(CancellationToken ct = default);
    Task<Contract?> GetAsync(int id, CancellationToken ct = default);
    Task<Contract> CreateAsync(Contract contract, IFormFile signedAgreement, CancellationToken ct = default);
    Task UpdateAsync(Contract contract, CancellationToken ct = default);
    Task<Contract> ChangeStatusAsync(int id, ContractStatus status, CancellationToken ct = default);
    Task UploadAgreementAsync(int id, IFormFile signedAgreement, CancellationToken ct = default);
    Task<FileDownload> DownloadAgreementAsync(int id, CancellationToken ct = default);
}

public interface IServiceRequestsApi
{
    Task<IReadOnlyList<ServiceRequest>> GetAllAsync(CancellationToken ct = default);
    Task<ServiceRequest?> GetAsync(int id, CancellationToken ct = default);
    Task<ServiceRequest> CreateAsync(int contractId, string description, decimal costUsd, CancellationToken ct = default);
    Task<ServiceRequest> ChangeStatusAsync(int id, ServiceRequestStatus status, CancellationToken ct = default);
    Task<ExchangeRateResult> GetUsdToZarRateAsync(CancellationToken ct = default);
}

public interface IDashboardApi
{
    Task<DashboardViewModel> GetAsync(CancellationToken ct = default);
}

public sealed class AuthApi : ApiClientBase, IAuthApi
{
    public AuthApi(HttpClient http) : base(http) { }

    public async Task<LoginResult?> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        try
        {
            return await PostJsonAsync<LoginResult>("api/auth/login", new { username, password }, ct);
        }
        catch (ApiUnauthorizedException)
        {
            return null;
        }
    }

    public Task<LoginResult> RegisterAsync(string username, string password, CancellationToken ct = default) =>
        PostJsonAsync<LoginResult>("api/auth/register", new { username, password }, ct);
}

public sealed class UsersApi : ApiClientBase, IUsersApi
{
    public UsersApi(HttpClient http) : base(http) { }

    public async Task<IReadOnlyList<UserAccount>> GetAllAsync(CancellationToken ct = default) =>
        await GetAsync<List<UserAccount>>("api/users", ct);

    public Task<UserAccount> UpdateAccessAsync(int id, string role, int? clientId, CancellationToken ct = default) =>
        PatchJsonAsync<UserAccount>($"api/users/{id}", new { role, clientId }, ct);

    public Task DeleteAsync(int id, CancellationToken ct = default) =>
        DeleteAsync($"api/users/{id}", ct);
}

public sealed class ClientsApi : ApiClientBase, IClientsApi
{
    public ClientsApi(HttpClient http) : base(http) { }

    public async Task<IReadOnlyList<Client>> GetAllAsync(CancellationToken ct = default) =>
        await GetAsync<List<Client>>("api/clients", ct);

    public async Task<Client?> GetAsync(int id, CancellationToken ct = default)
    {
        try
        {
            return await GetAsync<Client>($"api/clients/{id}", ct);
        }
        catch (ApiNotFoundException)
        {
            return null;
        }
    }

    public Task<Client> CreateAsync(Client client, CancellationToken ct = default) =>
        PostJsonAsync<Client>("api/clients", ToBody(client), ct);

    public Task UpdateAsync(Client client, CancellationToken ct = default) =>
        PutJsonAsync($"api/clients/{client.Id}", ToBody(client), ct);

    public Task DeleteAsync(int id, CancellationToken ct = default) =>
        DeleteAsync($"api/clients/{id}", ct);

    private static object ToBody(Client c) => new { c.Name, c.ContactDetails, c.Region };
}

public sealed class ContractsApi : ApiClientBase, IContractsApi
{
    public ContractsApi(HttpClient http) : base(http) { }

    public async Task<IReadOnlyList<Contract>> SearchAsync(ContractSearchCriteria? criteria, CancellationToken ct = default) =>
        await GetAsync<List<Contract>>("api/contracts" + ToQuery(criteria), ct);

    public async Task<IReadOnlyList<Contract>> GetOpenAsync(CancellationToken ct = default) =>
        await GetAsync<List<Contract>>("api/contracts/open", ct);

    public async Task<Contract?> GetAsync(int id, CancellationToken ct = default)
    {
        try
        {
            return await GetAsync<Contract>($"api/contracts/{id}", ct);
        }
        catch (ApiNotFoundException)
        {
            return null;
        }
    }

    public Task<Contract> CreateAsync(Contract contract, IFormFile signedAgreement, CancellationToken ct = default) =>
        SendAsync<Contract>(() =>
        {
            var form = new MultipartFormDataContent
            {
                { new StringContent(contract.ClientId.ToString()), "ClientId" },
                { new StringContent(contract.StartDate.ToString("yyyy-MM-dd")), "StartDate" },
                { new StringContent(contract.EndDate.ToString("yyyy-MM-dd")), "EndDate" },
                { new StringContent(contract.Status.ToString()), "Status" },
                { new StringContent(contract.ServiceLevel.ToString()), "ServiceLevel" }
            };
            form.Add(FileContent(signedAgreement), "SignedAgreement", signedAgreement.FileName);
            return new HttpRequestMessage(HttpMethod.Post, "api/contracts") { Content = form };
        }, ct);

    public Task UpdateAsync(Contract contract, CancellationToken ct = default) =>
        PutJsonAsync($"api/contracts/{contract.Id}", new
        {
            contract.ClientId,
            StartDate = contract.StartDate.Date,
            EndDate = contract.EndDate.Date,
            contract.ServiceLevel
        }, ct);

    public Task<Contract> ChangeStatusAsync(int id, ContractStatus status, CancellationToken ct = default) =>
        PatchJsonAsync<Contract>($"api/contracts/{id}/status", new { status }, ct);

    public Task UploadAgreementAsync(int id, IFormFile signedAgreement, CancellationToken ct = default) =>
        SendAsync(() =>
        {
            var form = new MultipartFormDataContent();
            form.Add(FileContent(signedAgreement), "signedAgreement", signedAgreement.FileName);
            return new HttpRequestMessage(HttpMethod.Post, $"api/contracts/{id}/agreement") { Content = form };
        }, ct);

    public async Task<FileDownload> DownloadAgreementAsync(int id, CancellationToken ct = default)
    {
        // Stream the PDF straight through without buffering it all in memory.
        var response = await SendRawAsync(new HttpRequestMessage(HttpMethod.Get, $"api/contracts/{id}/agreement"), ct, HttpCompletionOption.ResponseHeadersRead);
        var stream = await response.Content.ReadAsStreamAsync(ct);
        var name = response.Content.Headers.ContentDisposition?.FileNameStar
                   ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                   ?? $"Contract-{id}-Agreement.pdf";
        return new FileDownload(stream, name, response.Content.Headers.ContentType?.MediaType ?? "application/pdf", response);
    }

    private static StreamContent FileContent(IFormFile file)
    {
        var content = new StreamContent(file.OpenReadStream());
        content.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
        return content;
    }

    private static string ToQuery(ContractSearchCriteria? c)
    {
        if (c is null) return string.Empty;

        var parts = new List<string>();
        if (c.FromDate.HasValue) parts.Add($"fromDate={c.FromDate.Value:yyyy-MM-dd}");
        if (c.ToDate.HasValue) parts.Add($"toDate={c.ToDate.Value:yyyy-MM-dd}");
        if (c.Status.HasValue) parts.Add($"status={c.Status.Value}");
        if (!string.IsNullOrWhiteSpace(c.ClientName)) parts.Add($"clientName={Uri.EscapeDataString(c.ClientName.Trim())}");
        return parts.Count == 0 ? string.Empty : "?" + string.Join('&', parts);
    }
}

public sealed class ServiceRequestsApi : ApiClientBase, IServiceRequestsApi
{
    public ServiceRequestsApi(HttpClient http) : base(http) { }

    public async Task<IReadOnlyList<ServiceRequest>> GetAllAsync(CancellationToken ct = default) =>
        await GetAsync<List<ServiceRequest>>("api/service-requests", ct);

    public async Task<ServiceRequest?> GetAsync(int id, CancellationToken ct = default)
    {
        try
        {
            return await GetAsync<ServiceRequest>($"api/service-requests/{id}", ct);
        }
        catch (ApiNotFoundException)
        {
            return null;
        }
    }

    public Task<ServiceRequest> CreateAsync(int contractId, string description, decimal costUsd, CancellationToken ct = default) =>
        PostJsonAsync<ServiceRequest>("api/service-requests", new { contractId, description, costUsd }, ct);

    public Task<ServiceRequest> ChangeStatusAsync(int id, ServiceRequestStatus status, CancellationToken ct = default) =>
        PatchJsonAsync<ServiceRequest>($"api/service-requests/{id}/status", new { status }, ct);

    public Task<ExchangeRateResult> GetUsdToZarRateAsync(CancellationToken ct = default) =>
        GetAsync<ExchangeRateResult>("api/exchange-rates/usd-zar", ct);
}

public sealed class DashboardApi : ApiClientBase, IDashboardApi
{
    public DashboardApi(HttpClient http) : base(http) { }

    public Task<DashboardViewModel> GetAsync(CancellationToken ct = default) =>
        GetAsync<DashboardViewModel>("api/dashboard", ct);
}
