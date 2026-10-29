using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TechMove.Api.Dtos;

namespace TechMove.Tests.Integration;

[Collection(ApiCollection.Name)]
[Trait("Category", "Integration")]
public class AuthAndSystemEndpointTests
{
    private readonly ApiFixture _api;

    public AuthAndSystemEndpointTests(ApiFixture api) => _api = api;

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithToken()
    {
        using var client = _api.CreateClient();

        var response = await client.PostAsJsonAsync("api/auth/login", new { username = ApiFixture.AdminUser, password = ApiFixture.AdminPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponseDto>(ApiFixture.Json);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
        Assert.Equal("Admin", body.Role);
        Assert.True(body.ExpiresAtUtc > DateTime.UtcNow);
    }

    [Theory]
    [InlineData("admin", "wrong-password")]
    [InlineData("nobody", "Admin@123")]
    public async Task Login_BadCredentials_Returns401(string username, string password)
    {
        using var client = _api.CreateClient();

        var response = await client.PostAsJsonAsync("api/auth/login", new { username, password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_MissingFields_Returns400()
    {
        using var client = _api.CreateClient();

        var response = await client.PostAsJsonAsync("api/auth/login", new { username = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("api/contracts")]
    [InlineData("api/clients")]
    [InlineData("api/service-requests")]
    [InlineData("api/dashboard")]
    public async Task ProtectedEndpoints_WithoutToken_Return401(string url)
    {
        using var client = _api.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithTamperedToken_Returns401()
    {
        using var client = _api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJoYWNrZXIifQ.invalid-signature");

        var response = await client.GetAsync("api/contracts");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Health_Returns200()
    {
        using var client = _api.CreateClient();

        var response = await client.GetAsync("health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_DocumentIsPublishedAndListsEndpoints()
    {
        using var client = _api.CreateClient();

        var response = await client.GetAsync("swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("/api/contracts", json);
        Assert.Contains("/api/contracts/{id}/status", json);
        Assert.Contains("/api/service-requests", json);
    }

    [Fact]
    public async Task Dashboard_ReturnsCounts()
    {
        using var client = await _api.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("api/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dashboard = await response.Content.ReadFromJsonAsync<DashboardDto>(ApiFixture.Json);
        Assert.NotNull(dashboard);
        Assert.True(dashboard!.ClientCount >= 3);
    }

    [Fact]
    public async Task ExchangeRate_ReturnsPositiveUsdToZarRate()
    {
        using var client = await _api.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("api/exchange-rates/usd-zar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rate = await response.Content.ReadFromJsonAsync<ExchangeRateDto>(ApiFixture.Json);
        Assert.NotNull(rate);
        Assert.Equal("USD", rate!.Base);
        Assert.Equal("ZAR", rate.Target);
        Assert.True(rate.Rate > 0);
    }
}
