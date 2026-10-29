using System.Net;
using System.Net.Http.Json;
using TechMove.Api.Dtos;
using TechMove.Api.Models;

namespace TechMove.Tests.Integration;

/// <summary>
/// Sign-up and role-based access: Admin vs Staff vs Customer, and customers only ever
/// seeing their own company's data.
/// </summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Integration")]
public class RoleAccessEndpointTests
{
    private readonly ApiFixture _api;

    public RoleAccessEndpointTests(ApiFixture api) => _api = api;

    private static string NewUsername() => "user_" + Guid.NewGuid().ToString("N")[..10];

    // ---------------- Sign-up ----------------

    [Fact]
    public async Task Register_NewUser_Returns201_AsUnlinkedCustomer_WhoSeesNoContracts()
    {
        using var anonymous = _api.CreateClient();
        var username = NewUsername();

        var response = await anonymous.PostAsJsonAsync("api/auth/register", new { username, password = "Secret123" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var login = await response.Content.ReadFromJsonAsync<LoginResponseDto>(ApiFixture.Json);
        Assert.Equal("Customer", login!.Role);
        Assert.Null(login.ClientId);

        // Not linked to a company yet → sees nothing
        using var client = await _api.CreateAuthenticatedClientAsync(username, "Secret123");
        var contracts = await client.GetFromJsonAsync<List<ContractDto>>("api/contracts", ApiFixture.Json);
        Assert.NotNull(contracts);
        Assert.Empty(contracts!);
    }

    [Fact]
    public async Task Register_ExistingUsername_Returns409()
    {
        using var anonymous = _api.CreateClient();

        var response = await anonymous.PostAsJsonAsync("api/auth/register", new { username = "admin", password = "Secret123" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("short1A")]      // too short
    [InlineData("alllowercase1")] // no upper case
    [InlineData("NoNumbersHere")] // no digit
    public async Task Register_WeakPassword_Returns400(string password)
    {
        using var anonymous = _api.CreateClient();

        var response = await anonymous.PostAsJsonAsync("api/auth/register", new { username = NewUsername(), password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Me_ReturnsSignedInProfile()
    {
        using var client = await _api.CreateAuthenticatedClientAsync(ApiFixture.CustomerUser, ApiFixture.CustomerPassword);

        var me = await client.GetFromJsonAsync<UserDto>("api/auth/me", ApiFixture.Json);

        Assert.Equal("acme", me!.Username);
        Assert.Equal(UserRole.Customer, me.Role);
        Assert.Equal(1, me.ClientId);
    }

    // ---------------- Customer scope ----------------

    [Fact]
    public async Task Customer_SeesOnlyOwnCompanyContracts_EvenWhenAskingForAnother()
    {
        using var client = await _api.CreateAuthenticatedClientAsync(ApiFixture.CustomerUser, ApiFixture.CustomerPassword);

        var mine = await client.GetFromJsonAsync<List<ContractDto>>("api/contracts", ApiFixture.Json);
        var tryOther = await client.GetFromJsonAsync<List<ContractDto>>("api/contracts?clientId=2", ApiFixture.Json);

        Assert.NotEmpty(mine!);
        Assert.All(mine!, c => Assert.Equal(1, c.ClientId));
        Assert.All(tryOther!, c => Assert.Equal(1, c.ClientId));
    }

    [Theory]
    [InlineData("api/contracts/2")]           // Cape Freight's contract
    [InlineData("api/contracts/3")]           // Durban Port's contract
    [InlineData("api/contracts/2/agreement")] // someone else's PDF
    [InlineData("api/clients/2")]             // someone else's company
    public async Task Customer_OtherCompanysData_Returns404(string url)
    {
        using var client = await _api.CreateAuthenticatedClientAsync(ApiFixture.CustomerUser, ApiFixture.CustomerPassword);

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Customer_CanReadOwnContractAndCompany()
    {
        using var client = await _api.CreateAuthenticatedClientAsync(ApiFixture.CustomerUser, ApiFixture.CustomerPassword);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("api/contracts/1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("api/clients/1")).StatusCode);
    }

    [Fact]
    public async Task Customer_CannotListClients_CreateContracts_OrChangeStatuses_Returns403()
    {
        using var client = await _api.CreateAuthenticatedClientAsync(ApiFixture.CustomerUser, ApiFixture.CustomerPassword);
        using var form = ApiFixture.ContractForm(1, DateTime.Today, DateTime.Today.AddYears(1), ContractStatus.Draft, TestFiles.PdfBytes);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("api/clients")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("api/contracts", form)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PatchAsJsonAsync("api/contracts/4/status", new { status = "Active" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("api/users")).StatusCode);
    }

    [Fact]
    public async Task Customer_CanLogServiceRequestOnOwnContract_ButNotOnAnotherCompanys()
    {
        using var admin = await _api.CreateAuthenticatedClientAsync();
        var otherCompanyContract = await _api.CreateContractAsync(admin, ContractStatus.Active, clientId: 2);
        using var customer = await _api.CreateAuthenticatedClientAsync(ApiFixture.CustomerUser, ApiFixture.CustomerPassword);

        var own = await customer.PostAsJsonAsync("api/service-requests", new { contractId = 1, description = "Customer-logged request", costUsd = 20m });
        var other = await customer.PostAsJsonAsync("api/service-requests", new { contractId = otherCompanyContract.Id, description = "Not my contract", costUsd = 20m });

        Assert.Equal(HttpStatusCode.Created, own.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);

        var mine = await customer.GetFromJsonAsync<List<ServiceRequestDto>>("api/service-requests", ApiFixture.Json);
        Assert.All(mine!, r => Assert.Equal(1, r.Contract!.Client!.Id));
    }

    // ---------------- Staff vs Admin ----------------

    [Fact]
    public async Task Staff_CanListClients_ButCannotDeleteClients_OrManageUsers()
    {
        using var staff = await _api.CreateAuthenticatedClientAsync(ApiFixture.StaffUser, ApiFixture.StaffPassword);

        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("api/clients")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.DeleteAsync("api/clients/3")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("api/users")).StatusCode);
    }

    [Fact]
    public async Task Admin_LinksNewCustomerToCompany_CustomerThenSeesThatCompanysContracts()
    {
        using var anonymous = _api.CreateClient();
        var username = NewUsername();
        (await anonymous.PostAsJsonAsync("api/auth/register", new { username, password = "Secret123" })).EnsureSuccessStatusCode();

        using var admin = await _api.CreateAuthenticatedClientAsync();
        var users = await admin.GetFromJsonAsync<List<UserDto>>("api/users", ApiFixture.Json);
        var newUser = users!.Single(u => u.Username == username);

        var link = await admin.PatchAsJsonAsync($"api/users/{newUser.Id}", new { role = "Customer", clientId = 2 });
        Assert.Equal(HttpStatusCode.OK, link.StatusCode);
        var linked = await link.Content.ReadFromJsonAsync<UserDto>(ApiFixture.Json);
        Assert.Equal(2, linked!.ClientId);
        Assert.Equal("Cape Freight Co", linked.ClientName);

        // Signing in again picks up the new company
        using var customer = await _api.CreateFreshlyAuthenticatedClientAsync(username, "Secret123");
        var contracts = await customer.GetFromJsonAsync<List<ContractDto>>("api/contracts", ApiFixture.Json);
        Assert.NotEmpty(contracts!);
        Assert.All(contracts!, c => Assert.Equal(2, c.ClientId));
    }

    [Fact]
    public async Task Admin_CannotChangeOwnRole_OrDeleteSelf()
    {
        using var admin = await _api.CreateAuthenticatedClientAsync();
        var me = await admin.GetFromJsonAsync<UserDto>("api/auth/me", ApiFixture.Json);

        var demote = await admin.PatchAsJsonAsync($"api/users/{me!.Id}", new { role = "Staff" });
        var delete = await admin.DeleteAsync($"api/users/{me.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, demote.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, delete.StatusCode);
    }
}
