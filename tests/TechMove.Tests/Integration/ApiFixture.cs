using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TechMove.Api.Data;
using TechMove.Api.Dtos;
using TechMove.Api.Models;
using TechMove.Api.Services.Currency;
using TechMove.Api.Services.Files;

namespace TechMove.Tests.Integration;

/// <summary>Currency API stand-in so the in-process tests are fast, offline and predictable.</summary>
public sealed class FakeExchangeRateProvider : IExchangeRateProvider
{
    public decimal Rate { get; set; } = 18.50m;
    public bool Available { get; set; } = true;

    public Task<ExchangeRateResult> GetUsdToZarRateAsync(CancellationToken ct = default) =>
        Task.FromResult(Available
            ? ExchangeRateResult.Ok(Rate, DateTime.UtcNow)
            : ExchangeRateResult.Fail("The currency service is currently unavailable (simulated outage)."));
}

/// <summary>
/// Hosts the real API (real controllers, services, repositories, JWT auth, validation, Swagger)
/// in memory. Only three things are swapped: SQL Server → EF InMemory, the currency API → a fake,
/// and the file server folder → a temp folder.
/// </summary>
public sealed class TechMoveApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = "techmove-it-" + Guid.NewGuid().ToString("N");
    private readonly string _fileRoot = Path.Combine(Path.GetTempPath(), "techmove-it-" + Guid.NewGuid().ToString("N"));

    public FakeExchangeRateProvider Rates { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(_databaseName));

            services.RemoveAll<IExchangeRateProvider>();
            services.AddSingleton<IExchangeRateProvider>(Rates);

            services.RemoveAll<IFileStorageService>();
            services.AddSingleton<IFileStorageService>(new LocalFileStorageService(_fileRoot));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_fileRoot))
        {
            Directory.Delete(_fileRoot, recursive: true);
        }
    }
}

/// <summary>
/// Gives each test an HttpClient for the API.
/// By default the API runs in-process (WebApplicationFactory). Set the environment variable
/// TECHMOVE_API_URL (e.g. http://localhost:5001) to run the SAME tests against the API running
/// in Docker - that is what the GitHub Actions "docker" job does.
/// </summary>
public sealed class ApiFixture : IDisposable
{
    public const string AdminUser = "admin";
    public const string AdminPassword = "Admin@123";
    public const string StaffUser = "staff";
    public const string StaffPassword = "Staff@123";
    public const string CustomerUser = "acme";          // seeded Customer of client 1 (Acme Logistics)
    public const string CustomerPassword = "Customer@123";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly Lazy<TechMoveApiFactory> _factory = new(() => new TechMoveApiFactory());
    private readonly Dictionary<string, string> _tokens = new();

    public ApiFixture()
    {
        var url = Environment.GetEnvironmentVariable("TECHMOVE_API_URL");
        LiveBaseUrl = string.IsNullOrWhiteSpace(url) ? null : new Uri(url.EndsWith('/') ? url : url + "/");
    }

    /// <summary>Set when the tests are calling a real, running API (e.g. the Docker container).</summary>
    public Uri? LiveBaseUrl { get; }

    public bool IsLive => LiveBaseUrl is not null;

    /// <summary>The fake currency provider (only meaningful when running in-process).</summary>
    public FakeExchangeRateProvider? Rates => IsLive ? null : _factory.Value.Rates;

    public HttpClient CreateClient()
    {
        var client = IsLive ? new HttpClient { BaseAddress = LiveBaseUrl } : _factory.Value.CreateClient();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(string username = AdminUser, string password = AdminPassword)
    {
        var client = CreateClient();
        if (!_tokens.TryGetValue(username, out var token))
        {
            using var anonymous = CreateClient();
            var response = await anonymous.PostAsJsonAsync("api/auth/login", new { username, password });
            response.EnsureSuccessStatusCode();
            var login = await response.Content.ReadFromJsonAsync<LoginResponseDto>(Json);
            token = login!.Token;
            _tokens[username] = token;
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Signs in without using the token cache (e.g. after a user's role was changed).</summary>
    public async Task<HttpClient> CreateFreshlyAuthenticatedClientAsync(string username, string password)
    {
        _tokens.Remove(username);
        return await CreateAuthenticatedClientAsync(username, password);
    }

    public static MultipartFormDataContent ContractForm(
        int clientId, DateTime start, DateTime end, ContractStatus status,
        byte[]? file = null, string fileName = "signed-agreement.pdf", ServiceLevel level = ServiceLevel.Gold)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(clientId.ToString()), "ClientId" },
            { new StringContent(start.ToString("yyyy-MM-dd")), "StartDate" },
            { new StringContent(end.ToString("yyyy-MM-dd")), "EndDate" },
            { new StringContent(status.ToString()), "Status" },
            { new StringContent(level.ToString()), "ServiceLevel" }
        };

        if (file is not null)
        {
            var content = new ByteArrayContent(file);
            content.Headers.ContentType = new MediaTypeHeaderValue(fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "application/pdf" : "application/octet-stream");
            form.Add(content, "SignedAgreement", fileName);
        }

        return form;
    }

    /// <summary>Creates a fresh contract through the API (so tests don't depend on each other's data).</summary>
    public async Task<ContractDto> CreateContractAsync(HttpClient client, ContractStatus status = ContractStatus.Active, int clientId = 1)
    {
        var today = DateTime.Today;
        using var form = ContractForm(clientId, today.AddDays(-7), today.AddYears(1), status, TestFiles.PdfBytes);
        var response = await client.PostAsync("api/contracts", form);
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ContractDto>(Json))!;
    }

    public void Dispose()
    {
        if (_factory.IsValueCreated)
        {
            _factory.Value.Dispose();
        }
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "TechMove API";
}
