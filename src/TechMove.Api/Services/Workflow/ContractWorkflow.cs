using TechMove.Api.Models;

namespace TechMove.Api.Services.Workflow;

public interface IContractWorkflow
{
    bool CanCreateServiceRequest(Contract contract, DateTime today);
    void EnsureCanCreateServiceRequest(Contract contract, DateTime today);
    bool CanTransition(ContractStatus from, ContractStatus to);
    void EnsureCanTransition(ContractStatus from, ContractStatus to);
    IReadOnlyList<ContractStatus> AllowedNextStatuses(ContractStatus current);
}

/// <summary>
/// State-machine for the contract life cycle. All status rules live here (not in controllers):
///
///   Draft  -> Active | On Hold | Expired
///   Active -> On Hold | Expired
///   On Hold-> Active | Expired
///   Expired-> (final - no changes)
///
/// Service requests are blocked when the contract is Expired or On Hold,
/// and also when its End Date has already passed (it is expired in practice).
/// </summary>
public sealed class ContractWorkflow : IContractWorkflow
{
    private static readonly IReadOnlyDictionary<ContractStatus, ContractStatus[]> Transitions =
        new Dictionary<ContractStatus, ContractStatus[]>
        {
            [ContractStatus.Draft] = new[] { ContractStatus.Active, ContractStatus.OnHold, ContractStatus.Expired },
            [ContractStatus.Active] = new[] { ContractStatus.OnHold, ContractStatus.Expired },
            [ContractStatus.OnHold] = new[] { ContractStatus.Active, ContractStatus.Expired },
            [ContractStatus.Expired] = Array.Empty<ContractStatus>()
        };

    private static readonly ContractStatus[] BlockedForRequests = { ContractStatus.Expired, ContractStatus.OnHold };

    public bool CanCreateServiceRequest(Contract contract, DateTime today) =>
        GetBlockReason(contract, today) is null;

    public void EnsureCanCreateServiceRequest(Contract contract, DateTime today)
    {
        var reason = GetBlockReason(contract, today);
        if (reason is not null)
        {
            throw new BusinessRuleException(reason);
        }
    }

    public bool CanTransition(ContractStatus from, ContractStatus to) =>
        from == to || (Transitions.TryGetValue(from, out var next) && next.Contains(to));

    public void EnsureCanTransition(ContractStatus from, ContractStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new BusinessRuleException($"A contract cannot move from '{Label(from)}' to '{Label(to)}'.");
        }
    }

    public IReadOnlyList<ContractStatus> AllowedNextStatuses(ContractStatus current) =>
        new[] { current }.Concat(Transitions[current]).ToList();

    private static string? GetBlockReason(Contract? contract, DateTime today)
    {
        if (contract is null)
        {
            return "The selected contract does not exist.";
        }

        if (BlockedForRequests.Contains(contract.Status))
        {
            return $"Service requests cannot be created for a contract that is '{Label(contract.Status)}'.";
        }

        if (contract.EndDate.Date < today.Date)
        {
            return $"Service requests cannot be created: this contract ended on {contract.EndDate:yyyy-MM-dd}.";
        }

        return null;
    }

    private static string Label(ContractStatus s) => s == ContractStatus.OnHold ? "On Hold" : s.ToString();
}
