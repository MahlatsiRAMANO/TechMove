using TechMove.Api.Data.Repositories;
using TechMove.Api.Models;
using TechMove.Api.Services.Files;
using TechMove.Api.Services.Workflow;

namespace TechMove.Api.Services;

public sealed record AgreementDownload(Stream Content, string FileName);

public interface IContractService
{
    Task<IReadOnlyList<Contract>> SearchAsync(ContractSearchCriteria? criteria, CancellationToken ct = default);
    Task<Contract?> GetDetailsAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Contract>> GetOpenForServiceRequestsAsync(CancellationToken ct = default);
    Task<Contract> CreateAsync(Contract contract, IFormFile? signedAgreement, CancellationToken ct = default);
    Task UpdateAsync(Contract contract, CancellationToken ct = default);
    Task<Contract> ChangeStatusAsync(int contractId, ContractStatus newStatus, CancellationToken ct = default);
    Task UploadAgreementAsync(int contractId, IFormFile? signedAgreement, CancellationToken ct = default);
    Task<AgreementDownload> GetAgreementAsync(int contractId, CancellationToken ct = default);
}

/// <summary>Service layer: contract business logic. Controllers only call this - never the DbContext.</summary>
public sealed class ContractService : IContractService
{
    private readonly IContractRepository _contracts;
    private readonly IClientRepository _clients;
    private readonly IFileValidator _fileValidator;
    private readonly IFileStorageService _storage;
    private readonly IContractWorkflow _workflow;
    private readonly IDateTimeProvider _clock;

    public ContractService(
        IContractRepository contracts,
        IClientRepository clients,
        IFileValidator fileValidator,
        IFileStorageService storage,
        IContractWorkflow workflow,
        IDateTimeProvider clock)
    {
        _contracts = contracts;
        _clients = clients;
        _fileValidator = fileValidator;
        _storage = storage;
        _workflow = workflow;
        _clock = clock;
    }

    public Task<IReadOnlyList<Contract>> SearchAsync(ContractSearchCriteria? criteria, CancellationToken ct = default) =>
        _contracts.SearchAsync(criteria, ct);

    public Task<Contract?> GetDetailsAsync(int id, CancellationToken ct = default) =>
        _contracts.GetWithDetailsAsync(id, ct);

    public async Task<IReadOnlyList<Contract>> GetOpenForServiceRequestsAsync(CancellationToken ct = default)
    {
        var candidates = await _contracts.GetByStatusesAsync(new[] { ContractStatus.Draft, ContractStatus.Active }, ct);
        var today = _clock.Today;
        return candidates.Where(c => _workflow.CanCreateServiceRequest(c, today)).ToList();
    }

    public async Task<Contract> CreateAsync(Contract contract, IFormFile? signedAgreement, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ValidateDates(contract);

        if (await _clients.GetByIdAsync(contract.ClientId, ct) is null)
        {
            throw new BusinessRuleException("Please select a valid client.");
        }

        // Every contract needs its signed agreement - validate before anything is written.
        _fileValidator.Validate(signedAgreement);

        StoredFile stored;
        await using (var stream = signedAgreement!.OpenReadStream())
        {
            stored = await _storage.SaveAsync(stream, signedAgreement.FileName, ct);
        }

        contract.SignedAgreementFileName = stored.OriginalFileName;
        contract.SignedAgreementStoredName = stored.StoredFileName;
        contract.SignedAgreementUploadedAtUtc = _clock.UtcNow;

        try
        {
            await _contracts.AddAsync(contract, ct);
            await _contracts.SaveChangesAsync(ct);
        }
        catch
        {
            // Don't leave orphan files on the server if the database write fails.
            _storage.Delete(stored.StoredFileName);
            throw;
        }

        return contract;
    }

    public async Task UpdateAsync(Contract contract, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ValidateDates(contract);

        var existing = await _contracts.GetByIdAsync(contract.Id, ct)
                       ?? throw new NotFoundException($"Contract {contract.Id} was not found.");

        if (await _clients.GetByIdAsync(contract.ClientId, ct) is null)
        {
            throw new BusinessRuleException("Please select a valid client.");
        }

        _workflow.EnsureCanTransition(existing.Status, contract.Status);

        existing.ClientId = contract.ClientId;
        existing.StartDate = contract.StartDate.Date;
        existing.EndDate = contract.EndDate.Date;
        existing.ServiceLevel = contract.ServiceLevel;
        existing.Status = contract.Status;

        await _contracts.SaveChangesAsync(ct);
    }

    /// <summary>Moves a contract through the workflow (approve = Active, decline/suspend = On Hold, close = Expired).</summary>
    public async Task<Contract> ChangeStatusAsync(int contractId, ContractStatus newStatus, CancellationToken ct = default)
    {
        var contract = await _contracts.GetByIdAsync(contractId, ct)
                       ?? throw new NotFoundException($"Contract {contractId} was not found.");

        _workflow.EnsureCanTransition(contract.Status, newStatus);

        if (contract.Status != newStatus)
        {
            contract.Status = newStatus;
            await _contracts.SaveChangesAsync(ct);
        }

        return contract;
    }

    public async Task UploadAgreementAsync(int contractId, IFormFile? signedAgreement, CancellationToken ct = default)
    {
        var contract = await _contracts.GetByIdAsync(contractId, ct)
                       ?? throw new NotFoundException($"Contract {contractId} was not found.");

        _fileValidator.Validate(signedAgreement);

        StoredFile stored;
        await using (var stream = signedAgreement!.OpenReadStream())
        {
            stored = await _storage.SaveAsync(stream, signedAgreement.FileName, ct);
        }

        var previous = contract.SignedAgreementStoredName;
        contract.SignedAgreementFileName = stored.OriginalFileName;
        contract.SignedAgreementStoredName = stored.StoredFileName;
        contract.SignedAgreementUploadedAtUtc = _clock.UtcNow;

        try
        {
            await _contracts.SaveChangesAsync(ct);
        }
        catch
        {
            _storage.Delete(stored.StoredFileName);
            throw;
        }

        if (!string.IsNullOrEmpty(previous))
        {
            _storage.Delete(previous);
        }
    }

    public async Task<AgreementDownload> GetAgreementAsync(int contractId, CancellationToken ct = default)
    {
        var contract = await _contracts.GetByIdAsync(contractId, ct)
                       ?? throw new NotFoundException($"Contract {contractId} was not found.");

        if (string.IsNullOrEmpty(contract.SignedAgreementStoredName))
        {
            throw new NotFoundException("No signed agreement has been uploaded for this contract.");
        }

        var stream = _storage.OpenRead(contract.SignedAgreementStoredName);
        var name = string.IsNullOrWhiteSpace(contract.SignedAgreementFileName)
            ? $"Contract-{contract.Id}-Agreement.pdf"
            : contract.SignedAgreementFileName;

        return new AgreementDownload(stream, name);
    }

    private static void ValidateDates(Contract contract)
    {
        if (contract.StartDate == default || contract.EndDate == default)
        {
            throw new BusinessRuleException("Start date and end date are required.");
        }

        if (contract.EndDate.Date <= contract.StartDate.Date)
        {
            throw new BusinessRuleException("End date must be after the start date.");
        }
    }
}
