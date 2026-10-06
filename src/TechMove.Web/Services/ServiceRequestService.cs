using TechMove.Web.Data.Repositories;
using TechMove.Web.Models;
using TechMove.Web.Services.Currency;
using TechMove.Web.Services.Workflow;

namespace TechMove.Web.Services;

public interface IServiceRequestService
{
    Task<IReadOnlyList<ServiceRequest>> GetAllAsync(CancellationToken ct = default);
    Task<ServiceRequest?> GetAsync(int id, CancellationToken ct = default);
    Task<ServiceRequest> CreateAsync(int contractId, string? description, decimal costUsd, CancellationToken ct = default);
    Task UpdateStatusAsync(int id, ServiceRequestStatus newStatus, CancellationToken ct = default);
}

/// <summary>
/// Creates service requests: checks the contract workflow, fetches the live USD->ZAR rate,
/// converts the cost and saves both values plus the rate used (for auditability).
/// </summary>
public sealed class ServiceRequestService : IServiceRequestService
{
    public const decimal MaxCostUsd = 10_000_000m;

    private readonly IServiceRequestRepository _requests;
    private readonly IContractRepository _contracts;
    private readonly IContractWorkflow _workflow;
    private readonly IExchangeRateProvider _rates;
    private readonly ICurrencyConverter _converter;
    private readonly IServiceRequestFactory _factory;
    private readonly IDateTimeProvider _clock;

    public ServiceRequestService(
        IServiceRequestRepository requests,
        IContractRepository contracts,
        IContractWorkflow workflow,
        IExchangeRateProvider rates,
        ICurrencyConverter converter,
        IServiceRequestFactory factory,
        IDateTimeProvider clock)
    {
        _requests = requests;
        _contracts = contracts;
        _workflow = workflow;
        _rates = rates;
        _converter = converter;
        _factory = factory;
        _clock = clock;
    }

    public Task<IReadOnlyList<ServiceRequest>> GetAllAsync(CancellationToken ct = default) =>
        _requests.GetAllWithContractAsync(ct);

    public Task<ServiceRequest?> GetAsync(int id, CancellationToken ct = default) =>
        _requests.GetWithContractAsync(id, ct);

    public async Task<ServiceRequest> CreateAsync(int contractId, string? description, decimal costUsd, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new BusinessRuleException("A description is required.");
        }

        if (costUsd < 0)
        {
            throw new BusinessRuleException("Cost cannot be negative.");
        }

        if (costUsd > MaxCostUsd)
        {
            throw new BusinessRuleException($"Cost cannot exceed {MaxCostUsd:N0} USD.");
        }

        var contract = await _contracts.GetByIdAsync(contractId, ct)
                       ?? throw new NotFoundException($"Contract {contractId} was not found.");

        // Workflow rule: no requests on Expired / On Hold (or past end date) contracts.
        _workflow.EnsureCanCreateServiceRequest(contract, _clock.Today);

        var rate = await _rates.GetUsdToZarRateAsync(ct);
        if (!rate.Success)
        {
            throw new ExternalServiceException(rate.Error ?? "The exchange rate is unavailable right now.");
        }

        var costZar = _converter.ConvertUsdToZar(costUsd, rate.Rate);
        var request = _factory.Create(contract.Id, description, costUsd, rate.Rate, costZar, _clock.UtcNow);

        await _requests.AddAsync(request, ct);
        await _requests.SaveChangesAsync(ct);
        return request;
    }

    public async Task UpdateStatusAsync(int id, ServiceRequestStatus newStatus, CancellationToken ct = default)
    {
        var request = await _requests.GetByIdAsync(id, ct)
                      ?? throw new NotFoundException($"Service request {id} was not found.");

        if (request.Status == newStatus)
        {
            return;
        }

        if (request.Status is ServiceRequestStatus.Completed or ServiceRequestStatus.Cancelled)
        {
            throw new BusinessRuleException($"A {request.Status} request can no longer be changed.");
        }

        if (request.Status == ServiceRequestStatus.InProgress && newStatus == ServiceRequestStatus.Pending)
        {
            throw new BusinessRuleException("An in-progress request cannot go back to Pending.");
        }

        request.Status = newStatus;
        await _requests.SaveChangesAsync(ct);
    }
}
