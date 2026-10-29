using TechMove.Api.Data.Repositories;
using TechMove.Api.Dtos;
using TechMove.Api.Models;

namespace TechMove.Api.Services;

public interface IDashboardService
{
    Task<DashboardDto> GetSummaryAsync(CancellationToken ct = default);
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

    public async Task<DashboardDto> GetSummaryAsync(CancellationToken ct = default)
    {
        // Sequential awaits: one DbContext must not run queries in parallel.
        var clients = (await _clients.GetAllAsync(ct)).Count;
        var draft = await _contracts.CountByStatusAsync(ContractStatus.Draft, ct);
        var active = await _contracts.CountByStatusAsync(ContractStatus.Active, ct);
        var onHold = await _contracts.CountByStatusAsync(ContractStatus.OnHold, ct);
        var expired = await _contracts.CountByStatusAsync(ContractStatus.Expired, ct);
        var requests = await _requests.CountAsync(ct);
        var total = await _requests.TotalCostAsync(ct);

        return new DashboardDto(clients, draft, active, onHold, expired, requests, total);
    }
}
