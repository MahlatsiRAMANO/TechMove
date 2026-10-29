using TechMove.Api.Data.Repositories;
using TechMove.Api.Dtos;
using TechMove.Api.Models;

namespace TechMove.Api.Services;

public interface IDashboardService
{
    /// <summary>Counts for everything, or for one client when <paramref name="clientId"/> is set.</summary>
    Task<DashboardDto> GetSummaryAsync(int? clientId = null, CancellationToken ct = default);
}

public sealed class DashboardService : IDashboardService
{
    private readonly IClientRepository _clients;
    private readonly IContractRepository _contracts;
    private readonly IServiceRequestRepository _requests;

    public DashboardService(IClientRepository clients, IContractRepository contracts, IServiceRequestRepository requests)
    {
        _clients = clients;
        _contracts = contracts;
        _requests = requests;
    }

    public async Task<DashboardDto> GetSummaryAsync(int? clientId = null, CancellationToken ct = default)
    {
        // Sequential awaits: one DbContext must not run queries in parallel.
        var clients = clientId.HasValue
            ? (await _clients.GetByIdAsync(clientId.Value, ct) is null ? 0 : 1)
            : (await _clients.GetAllAsync(ct)).Count;
        var draft = await _contracts.CountByStatusAsync(ContractStatus.Draft, clientId, ct);
        var active = await _contracts.CountByStatusAsync(ContractStatus.Active, clientId, ct);
        var onHold = await _contracts.CountByStatusAsync(ContractStatus.OnHold, clientId, ct);
        var expired = await _contracts.CountByStatusAsync(ContractStatus.Expired, clientId, ct);
        var requests = await _requests.CountAsync(clientId, ct);
        var total = await _requests.TotalCostAsync(clientId, ct);

        return new DashboardDto(clients, draft, active, onHold, expired, requests, total);
    }
}
