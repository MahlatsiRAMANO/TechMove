using System.Net;
using System.Net.Http.Json;
using TechMove.Api.Dtos;
using TechMove.Api.Models;

namespace TechMove.Tests.Integration;

[Collection(ApiCollection.Name)]
[Trait("Category", "Integration")]
public class ServiceRequestsEndpointTests
{
    private readonly ApiFixture _api;

    public ServiceRequestsEndpointTests(ApiFixture api) => _api = api;

    [Fact]
    public async Task CreateServiceRequest_OnActiveContract_Returns201_CostConverted_ThenReadBack()
    {
        using var client = await _api.CreateAuthenticatedClientAsync();
        var contract = await _api.CreateContractAsync(client, ContractStatus.Active);

        // CREATE
        var response = await client.PostAsJsonAsync("api/service-requests",
            new { contractId = contract.Id, description = "Relocate 12 server racks", costUsd = 250.75m });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ServiceRequestDto>(ApiFixture.Json);
        Assert.NotNull(created);
        Assert.Equal(250.75m, created!.CostUsd);
        Assert.True(created.ExchangeRate > 0);
        Assert.Equal(Math.Round(250.75m * created.ExchangeRate, 2, MidpointRounding.AwayFromZero), created.Cost);
        if (!_api.IsLive)
        {
            Assert.Equal(4638.88m, created.Cost); // 250.75 × 18.50 (fake rate) = 4638.875 → 4638.88
        }

        // READ
        var read = await client.GetFromJsonAsync<ServiceRequestDto>(response.Headers.Location!, ApiFixture.Json);
        Assert.NotNull(read);
        Assert.Equal(created.Id, read!.Id);
        Assert.Equal("Relocate 12 server racks", read.Description);
        Assert.Equal(created.Cost, read.Cost);
        Assert.Equal(ServiceRequestStatus.Pending, read.Status);
        Assert.Equal(contract.Id, read.ContractId);

        // It shows on the contract too
        var withRequests = await client.GetFromJsonAsync<ContractDto>($"api/contracts/{contract.Id}", ApiFixture.Json);
        Assert.Contains(withRequests!.ServiceRequests, s => s.Id == created.Id);
    }

    [Theory]
    [InlineData(2)] // seeded Expired contract
    [InlineData(3)] // seeded On Hold contract
    public async Task CreateServiceRequest_OnExpiredOrOnHoldContract_Returns400(int contractId)
    {
        using var client = await _api.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("api/service-requests",
            new { contractId, description = "Should be blocked", costUsd = 10m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("cannot be created", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CreateServiceRequest_ZeroCost_Returns201WithZeroRand()
    {
        using var client = await _api.CreateAuthenticatedClientAsync();
        var contract = await _api.CreateContractAsync(client, ContractStatus.Active);

        var response = await client.PostAsJsonAsync("api/service-requests",
            new { contractId = contract.Id, description = "Free warranty call-out", costUsd = 0m });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ServiceRequestDto>(ApiFixture.Json);
        Assert.Equal(0m, created!.Cost);
    }

    [Theory]
    [InlineData(-5, "Valid description")]
    [InlineData(10, "")]
    [InlineData(10, null)]
    public async Task CreateServiceRequest_InvalidInput_Returns400(int costUsd, string? description)
    {
        using var client = await _api.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("api/service-requests",
            new { contractId = 1, description, costUsd = (decimal)costUsd });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateServiceRequest_UnknownContract_Returns404()
    {
        using var client = await _api.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("api/service-requests",
            new { contractId = 987654, description = "Nobody home", costUsd = 10m });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PatchServiceRequestStatus_ToCompleted_ThenCannotChangeAgain()
    {
        using var client = await _api.CreateAuthenticatedClientAsync();
        var contract = await _api.CreateContractAsync(client, ContractStatus.Active);
        var create = await client.PostAsJsonAsync("api/service-requests", new { contractId = contract.Id, description = "Install fibre link", costUsd = 99m });
        var request = await create.Content.ReadFromJsonAsync<ServiceRequestDto>(ApiFixture.Json);

        var completed = await client.PatchAsJsonAsync($"api/service-requests/{request!.Id}/status", new { status = "Completed" });
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);

        var reopen = await client.PatchAsJsonAsync($"api/service-requests/{request.Id}/status", new { status = "Pending" });
        Assert.Equal(HttpStatusCode.BadRequest, reopen.StatusCode);
    }
}

[Collection(ApiCollection.Name)]
[Trait("Category", "Integration")]
public class ClientsEndpointTests
{
    private readonly ApiFixture _api;

    public ClientsEndpointTests(ApiFixture api) => _api = api;

    [Fact]
    public async Task Client_CreateReadUpdateDelete_RoundTrip()
    {
        using var client = await _api.CreateAuthenticatedClientAsync();
        var name = "Integration Test Movers " + Guid.NewGuid().ToString("N")[..6];

        // CREATE → 201
        var create = await client.PostAsJsonAsync("api/clients", new { name, contactDetails = "it@test.co.za", region = "Limpopo" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<ClientDto>(ApiFixture.Json);

        // READ → same data
        var read = await client.GetFromJsonAsync<ClientDetailsDto>(create.Headers.Location!, ApiFixture.Json);
        Assert.Equal(name, read!.Name);
        Assert.Equal("Limpopo", read.Region);
        Assert.Empty(read.Contracts);

        // UPDATE → 204, then re-read
        var update = await client.PutAsJsonAsync($"api/clients/{created!.Id}", new { name, contactDetails = "new@test.co.za", region = "Free State" });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);
        var reread = await client.GetFromJsonAsync<ClientDetailsDto>($"api/clients/{created.Id}", ApiFixture.Json);
        Assert.Equal("Free State", reread!.Region);

        // DELETE → 204, then 404
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"api/clients/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"api/clients/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task CreateClient_MissingName_Returns400WithFieldError()
    {
        using var client = await _api.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("api/clients", new { name = "", contactDetails = "x", region = "y" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Name", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DeleteClient_WithContracts_Returns400()
    {
        using var client = await _api.CreateAuthenticatedClientAsync();

        var response = await client.DeleteAsync("api/clients/1"); // seeded client with contracts

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

/// <summary>
/// Always runs in-process, because it needs to simulate the external currency API going down.
/// </summary>
[Trait("Category", "Integration")]
public class CurrencyOutageTests : IClassFixture<TechMoveApiFactory>
{
    private readonly TechMoveApiFactory _factory;

    public CurrencyOutageTests(TechMoveApiFactory factory) => _factory = factory;

    [Fact]
    public async Task WhenCurrencyApiIsDown_CreateServiceRequest_Returns503_AndNothingIsSaved()
    {
        using var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("api/auth/login", new { username = ApiFixture.AdminUser, password = ApiFixture.AdminPassword });
        var token = (await login.Content.ReadFromJsonAsync<LoginResponseDto>(ApiFixture.Json))!.Token;
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var before = (await client.GetFromJsonAsync<List<ServiceRequestDto>>("api/service-requests", ApiFixture.Json))!.Count;

        _factory.Rates.Available = false;
        try
        {
            var response = await client.PostAsJsonAsync("api/service-requests", new { contractId = 1, description = "During outage", costUsd = 50m });
            var rate = await client.GetAsync("api/exchange-rates/usd-zar");

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, rate.StatusCode);
        }
        finally
        {
            _factory.Rates.Available = true;
        }

        var after = (await client.GetFromJsonAsync<List<ServiceRequestDto>>("api/service-requests", ApiFixture.Json))!.Count;
        Assert.Equal(before, after);
    }
}
