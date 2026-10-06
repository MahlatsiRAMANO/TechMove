using Microsoft.EntityFrameworkCore;
using TechMove.Web.Data;
using TechMove.Web.Data.Repositories;
using TechMove.Web.Models;
using TechMove.Web.Services;
using TechMove.Web.Services.Files;
using TechMove.Web.Services.Workflow;

namespace TechMove.Tests;

/// <summary>
/// Contract service against the EF Core InMemory provider and a temporary folder
/// as the "file server" - checks the upload/download flow end to end.
/// </summary>
public class ContractServiceTests : IDisposable
{
    private static readonly DateTime Today = new(2026, 10, 6);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "techmove-tests-" + Guid.NewGuid().ToString("N"));
    private readonly AppDbContext _db;
    private readonly LocalFileStorageService _storage;
    private readonly ContractService _sut;

    public ContractServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("techmove-" + Guid.NewGuid())
            .Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated(); // applies seed data (clients 1-3, contracts 1-4)

        _storage = new LocalFileStorageService(_folder);
        _sut = new ContractService(
            new ContractRepository(_db),
            new ClientRepository(_db),
            new PdfFileValidator(),
            _storage,
            new ContractWorkflow(),
            new FixedClock(Today));
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private static Contract NewContract() => new()
    {
        ClientId = 1,
        StartDate = Today,
        EndDate = Today.AddYears(1),
        Status = ContractStatus.Draft,
        ServiceLevel = ServiceLevel.Gold
    };

    [Fact]
    public async Task CreateAsync_WithPdf_SavesFileUnderGuidNameAndCanDownload()
    {
        var created = await _sut.CreateAsync(NewContract(), TestFiles.FormFile("agreement.pdf", TestFiles.PdfBytes));

        Assert.Equal("agreement.pdf", created.SignedAgreementFileName);
        Assert.NotEqual("agreement.pdf", created.SignedAgreementStoredName);
        Assert.True(_storage.Exists(created.SignedAgreementStoredName!));

        var download = await _sut.GetAgreementAsync(created.Id);
        await using var content = download.Content;
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms);
        Assert.Equal(TestFiles.PdfBytes, ms.ToArray());
        Assert.Equal("agreement.pdf", download.FileName);
    }

    [Fact]
    public async Task CreateAsync_TwoFilesWithSameName_DoNotOverwrite()
    {
        var a = await _sut.CreateAsync(NewContract(), TestFiles.FormFile("agreement.pdf", TestFiles.PdfBytes));
        var b = await _sut.CreateAsync(NewContract(), TestFiles.FormFile("agreement.pdf", TestFiles.PdfBytes));

        Assert.NotEqual(a.SignedAgreementStoredName, b.SignedAgreementStoredName);
        Assert.Equal(2, Directory.GetFiles(_folder).Length);
    }

    [Fact]
    public async Task CreateAsync_ExeUpload_ThrowsAndSavesNothing()
    {
        var before = await _db.Contracts.CountAsync();

        await Assert.ThrowsAsync<InvalidFileException>(() =>
            _sut.CreateAsync(NewContract(), TestFiles.FormFile("setup.exe", TestFiles.ExeBytes)));

        Assert.Equal(before, await _db.Contracts.CountAsync());
        Assert.Empty(Directory.GetFiles(_folder));
    }

    [Fact]
    public async Task CreateAsync_NoFile_Throws()
    {
        await Assert.ThrowsAsync<InvalidFileException>(() => _sut.CreateAsync(NewContract(), null));
    }

    [Fact]
    public async Task CreateAsync_EndBeforeStart_Throws()
    {
        var c = NewContract();
        c.EndDate = c.StartDate.AddDays(-1);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _sut.CreateAsync(c, TestFiles.FormFile("a.pdf", TestFiles.PdfBytes)));
    }

    [Fact]
    public async Task UpdateAsync_ExpiredContractCannotBeReactivated()
    {
        // Seed contract 2 is Expired.
        var update = new Contract
        {
            Id = 2, ClientId = 2,
            StartDate = new DateTime(2024, 1, 1), EndDate = new DateTime(2025, 12, 31),
            Status = ContractStatus.Active, ServiceLevel = ServiceLevel.Silver
        };

        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.UpdateAsync(update));
    }

    [Fact]
    public async Task GetOpenForServiceRequestsAsync_ExcludesExpiredAndOnHold()
    {
        var open = await _sut.GetOpenForServiceRequestsAsync();

        Assert.All(open, c => Assert.True(c.Status is ContractStatus.Active or ContractStatus.Draft));
        Assert.DoesNotContain(open, c => c.Id == 2); // Expired
        Assert.DoesNotContain(open, c => c.Id == 3); // On Hold
    }

    [Fact]
    public async Task GetAgreementAsync_NoFileUploaded_ThrowsNotFound()
    {
        // Seed contracts have no agreement.
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetAgreementAsync(1));
    }
}
