using TechMove.Api.Models;
using TechMove.Api.Services.Workflow;

namespace TechMove.Api.Dtos;

/// <summary>Maps entities to DTOs. Workflow answers (can request service, next statuses) are
/// calculated here so the frontend never has to re-implement business rules.</summary>
public sealed class DtoMapper
{
    private readonly IContractWorkflow _workflow;

    public DtoMapper(IContractWorkflow workflow) => _workflow = workflow;

    public ClientDto ToDto(Client c) => new(c.Id, c.Name, c.ContactDetails, c.Region, c.Contracts?.Count ?? 0);

    public ClientDetailsDto ToDetailsDto(Client c) => new(
        c.Id, c.Name, c.ContactDetails, c.Region,
        c.Contracts.OrderByDescending(x => x.StartDate).Select(ToSummary).ToList());

    public static ClientSummaryDto? ToSummary(Client? c) => c is null ? null : new(c.Id, c.Name, c.Region);

    public static ContractSummaryDto ToSummary(Contract c) => new(c.Id, c.StartDate, c.EndDate, c.Status, c.ServiceLevel);

    public ContractDto ToDto(Contract c, DateTime today) => new(
        c.Id,
        c.ClientId,
        ToSummary(c.Client),
        c.StartDate,
        c.EndDate,
        c.Status,
        c.ServiceLevel,
        c.HasSignedAgreement,
        c.SignedAgreementFileName,
        c.SignedAgreementUploadedAtUtc,
        _workflow.CanCreateServiceRequest(c, today),
        _workflow.AllowedNextStatuses(c.Status),
        (c.ServiceRequests ?? new List<ServiceRequest>())
            .OrderByDescending(s => s.CreatedAtUtc)
            .Select(s => ToRequestDto(s, includeContract: false))
            .ToList());

    public static ServiceRequestDto ToRequestDto(ServiceRequest s, bool includeContract = true) => new(
        s.Id,
        s.ContractId,
        s.Description,
        s.CostUsd,
        s.ExchangeRate,
        s.Cost,
        s.Status,
        s.CreatedAtUtc,
        includeContract && s.Contract is not null
            ? new ServiceRequestContractDto(s.Contract.Id, s.Contract.Status, ToSummary(s.Contract.Client))
            : null);
}
