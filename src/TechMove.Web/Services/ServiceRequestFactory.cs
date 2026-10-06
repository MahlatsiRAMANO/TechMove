using TechMove.Web.Models;

namespace TechMove.Web.Services;

/// <summary>
/// Factory pattern: one place that knows how a valid new ServiceRequest is assembled
/// (trimmed description, Pending status, UTC timestamp, both currencies and the rate used).
/// </summary>
public interface IServiceRequestFactory
{
    ServiceRequest Create(int contractId, string description, decimal costUsd, decimal exchangeRate, decimal costZar, DateTime createdAtUtc);
}

public sealed class ServiceRequestFactory : IServiceRequestFactory
{
    public ServiceRequest Create(int contractId, string description, decimal costUsd, decimal exchangeRate, decimal costZar, DateTime createdAtUtc) =>
        new()
        {
            ContractId = contractId,
            Description = description.Trim(),
            CostUsd = costUsd,
            ExchangeRate = exchangeRate,
            Cost = costZar,
            Status = ServiceRequestStatus.Pending,
            CreatedAtUtc = createdAtUtc
        };
}
