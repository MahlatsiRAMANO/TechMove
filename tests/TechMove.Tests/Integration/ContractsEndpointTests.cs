using System.Net;
using System.Net.Http.Json;
using TechMove.Api.Dtos;
using TechMove.Api.Models;

namespace TechMove.Tests.Integration;

[Collection(ApiCollection.Name)]
[Trait("Category", "Integration")]
public class ContractsEndpointTests
{
    private readonly ApiFixture _api;

    public ContractsEndpointTests(ApiFixture api) => _api = api;

    [Fact]
    public async Task GetContracts_Returns200AndJsonList()
    {
        using var client = await _api.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("api/contracts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var contracts = await response.Content.ReadFromJsonAsync<List<ContractDto>>(ApiFixture.Json);
        Assert.NotNull(contracts);
        Assert.NotEmpty(contracts!);
        Assert.All(contracts!, c => Assert.NotNull(c.Client));
    }

    [Theory]
    [InlineData(ContractStatus.Active)]
    [InlineData(ContractStatus.Expired)]
    [InlineData(ContractStatus.OnHold)]
    public async Task GetContracts_FilterByStatus_ReturnsOnlyThatStatus(ContractStatus status)
    {
        using var client = await _api.CreateAuthenticatedClientAsync();

        var contracts = await client.GetFromJsonAsync<List<ContractDto>>($"api/contracts?status={status}", ApiFixture.Json);

        Assert.NotNull(contracts);
        Assert.NotEmpty(contracts!);
        Assert.All(contracts!, c => Assert.Equal(status, c.Status));
    }

    [Fact]
    public async Task GetContracts_FilterByDateRange_ReturnsOnlyContractsInForce()
    {
        using var client = await _api.CreateAuthenticatedClientAsync();
        var from = new DateTime(2024, 6, 1);
        var to = new DateTime(2024, 6, 30);

        var contracts = await client.GetFromJsonAsync<List<ContractDto>>("api/contracts?fromDate=2024-06-01&toDate=2024-06-30", ApiFixture.Json);

        Assert.NotNull(contracts);
        Assert.Contains(contracts!, c => c.Id == 2); // seeded 2024-2025 contract
        Assert.All(contracts!, c => Assert.True(c.StartDate <= to && c.EndDate >= from));
    }

    [Fact]
    public async Task GetContract_UnknownId_Returns404ProblemDetails()
    {
        using var client = await _api.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("api/contracts/987654");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task CreateContract_WithPdf_Returns201_ThenReadBackReturnsSameData()
    {
        using var client = await _api.CreateAuthenticatedClientAsync();
        var start = DateTime.Today.AddDays(1);
        var end = start.AddYears(2);

        // CREATE
        using var form = ApiFixture.ContractForm(2, start, end, ContractStatus.Draft, TestFiles.PdfBytes, "Cape Freight signed.pdf", ServiceLevel.Platinum);
        var createResponse = await client.PostAsync("api/contracts", form);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(createResponse.Headers.Location);
        var created = await createResponse.Content.ReadFromJsonAsync<ContractDto>(ApiFixture.Json);
        Assert.NotNull(created);
        Assert.True(created!.Id > 0);

        // READ (using the Location header the API returned)
        var readResponse = await client.GetAsync(createResponse.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
        var read = await readResponse.Content.ReadFromJsonAsync<ContractDto>(ApiFixture.Json);

        // Data integrity: what we saved is exactly what we get back
        Assert.NotNull(read);
        Assert.Equal(created.Id, read!.Id);
        Assert.Equal(2, read.ClientId);
        Assert.Equal("Cape Freight Co", read.Client!.Name);
        Assert.Equal(start, read.StartDate.Date);
        Assert.Equal(end, read.EndDate.Date);
        Assert.Equal(ContractStatus.Draft, read.Status);
        Assert.Equal(ServiceLevel.Platinum, read.ServiceLevel);
        Assert.True(read.HasSignedAgreement);
        Assert.Equal("Cape Freight signed.pdf", read.SignedAgreementFileName);

        // The PDF on the file server is byte-for-byte what we uploaded
        var download = await client.GetAsync($"api/contracts/{created.Id}/agreement");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal(TestFiles.PdfBytes, await download.Content.ReadAsByteArrayAsync());

        // It also appears in the search results
        var all = await client.GetFromJsonAsync<List<ContractDto>>("api/contracts?clientName=Cape", ApiFixture.Json);
        Assert.Contains(all!, c => c.Id == created.Id);
    }

    [Fact]
    public async Task CreateContract_WithExeFile_Returns400()
    {
        using var client = await _api.CreateAuthenticatedClientAsync();

        using var form = ApiFixture.ContractForm(1, DateTime.Today, DateTime.Today.AddYears(1), ContractStatus.Draft, TestFiles.ExeBytes, "setup.exe");
        var response = await client.PostAsync("api/contracts", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(".exe", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CreateContract_ExeRenamedToPdf_Returns400()
    {
        using var client = await _api.CreateAuthenticatedClientAsync();

        using var form = ApiFixture.ContractForm(1, DateTime.Today, DateTime.Today.AddYears(1), ContractStatus.Draft, TestFiles.ExeBytes, "looks-legit.pdf");
        var response = await client.PostAsync("api/contracts", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateContract_WithoutFile_Returns400()
    {
        using var client = await _api.CreateAuthenticatedClientAsync();

        using var form = ApiFixture.ContractForm(1, DateTime.Today, DateTime.Today.AddYears(1), ContractStatus.Draft, file: null);
        var response = await client.PostAsync("api/contracts", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateContract_EndBeforeStart_Returns400()
    {
        using var client = await _api.CreateAuthenticatedClientAsync();

        using var form = ApiFixture.ContractForm(1, DateTime.Today, DateTime.Today.AddDays(-1), ContractStatus.Draft, TestFiles.PdfBytes);
        var response = await client.PostAsync("api/contracts", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateContract_UnknownClient_Returns400()
    {
        using var client = await _api.CreateAuthenticatedClientAsync();

        using var form = ApiFixture.ContractForm(999999, DateTime.Today, DateTime.Today.AddYears(1), ContractStatus.Draft, TestFiles.PdfBytes);
        var response = await client.PostAsync("api/contracts", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PatchStatus_DraftToActive_Returns200AndIsSaved()
    {
        using var client = await _api.CreateAuthenticatedClientAsync();
        var contract = await _api.CreateContractAsync(client, ContractStatus.Draft);

        var response = await client.PatchAsJsonAsync($"api/contracts/{contract.Id}/status", new { status = "Active" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reread = await client.GetFromJsonAsync<ContractDto>($"api/contracts/{contract.Id}", ApiFixture.Json);
        Assert.Equal(ContractStatus.Active, reread!.Status);
    }

    [Fact]
    public async Task PatchStatus_ExpiredToActive_Returns400()
    {
        using var client = await _api.CreateAuthenticatedClientAsync();
        var contract = await _api.CreateContractAsync(client, ContractStatus.Active);
        (await client.PatchAsJsonAsync($"api/contracts/{contract.Id}/status", new { status = "Expired" })).EnsureSuccessStatusCode();

        var response = await client.PatchAsJsonAsync($"api/contracts/{contract.Id}/status", new { status = "Active" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PatchStatus_InvalidValue_Returns400()
    {
        using var client = await _api.CreateAuthenticatedClientAsync();

        var response = await client.PatchAsJsonAsync("api/contracts/1/status", new { status = "Banana" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PatchStatus_AsStaff_Returns403()
    {
        using var staff = await _api.CreateAuthenticatedClientAsync(ApiFixture.StaffUser, ApiFixture.StaffPassword);

        var response = await staff.PatchAsJsonAsync("api/contracts/4/status", new { status = "Active" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PatchStatus_UnknownContract_Returns404()
    {
        using var client = await _api.CreateAuthenticatedClientAsync();

        var response = await client.PatchAsJsonAsync("api/contracts/987654/status", new { status = "Active" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
