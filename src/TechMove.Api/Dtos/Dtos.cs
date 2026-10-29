using System.ComponentModel.DataAnnotations;
using TechMove.Api.Models;

namespace TechMove.Api.Dtos;

// ----------------------------------------------------------------------------------------------
// Data Transfer Objects: the JSON contract of the API. Entities never leave the API directly,
// which avoids circular references (Client -> Contracts -> Client) and hides internal fields
// such as the GUID file name on the file server.
// ----------------------------------------------------------------------------------------------

public sealed record ClientDto(int Id, string Name, string ContactDetails, string Region, int ContractCount);

public sealed class ClientWriteDto
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string ContactDetails { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string Region { get; set; } = string.Empty;
}

public sealed record ClientDetailsDto(int Id, string Name, string ContactDetails, string Region, IReadOnlyList<ContractSummaryDto> Contracts);

public sealed record ClientSummaryDto(int Id, string Name, string Region);

public sealed record ContractSummaryDto(int Id, DateTime StartDate, DateTime EndDate, ContractStatus Status, ServiceLevel ServiceLevel);

public sealed record ContractDto(
    int Id,
    int ClientId,
    ClientSummaryDto? Client,
    DateTime StartDate,
    DateTime EndDate,
    ContractStatus Status,
    ServiceLevel ServiceLevel,
    bool HasSignedAgreement,
    string? SignedAgreementFileName,
    DateTime? SignedAgreementUploadedAtUtc,
    bool CanCreateServiceRequest,
    IReadOnlyList<ContractStatus> AllowedNextStatuses,
    IReadOnlyList<ServiceRequestDto> ServiceRequests);

/// <summary>Form fields for POST /api/contracts (sent as multipart/form-data with the PDF).</summary>
public sealed class ContractCreateForm
{
    [Required]
    public int? ClientId { get; set; }

    [Required]
    public DateTime? StartDate { get; set; }

    [Required]
    public DateTime? EndDate { get; set; }

    public ContractStatus Status { get; set; } = ContractStatus.Draft;

    public ServiceLevel ServiceLevel { get; set; } = ServiceLevel.Bronze;

    /// <summary>The signed agreement - PDF only, max 10 MB.</summary>
    [Required]
    public IFormFile? SignedAgreement { get; set; }
}

/// <summary>Body for PUT /api/contracts/{id}. Status is changed separately via PATCH /status.</summary>
public sealed class ContractUpdateDto
{
    [Required]
    public int? ClientId { get; set; }

    [Required]
    public DateTime? StartDate { get; set; }

    [Required]
    public DateTime? EndDate { get; set; }

    public ServiceLevel ServiceLevel { get; set; }
}

/// <summary>Body for PATCH /api/contracts/{id}/status, e.g. { "status": "Active" }.</summary>
public sealed class ContractStatusUpdateDto
{
    [Required]
    public ContractStatus? Status { get; set; }
}

/// <summary>Body for PATCH /api/service-requests/{id}/status.</summary>
public sealed class ServiceRequestStatusUpdateDto
{
    [Required]
    public ServiceRequestStatus? Status { get; set; }
}

public sealed record ServiceRequestDto(
    int Id,
    int ContractId,
    string Description,
    decimal CostUsd,
    decimal ExchangeRate,
    decimal Cost,
    ServiceRequestStatus Status,
    DateTime CreatedAtUtc,
    ServiceRequestContractDto? Contract);

public sealed record ServiceRequestContractDto(int Id, ContractStatus Status, ClientSummaryDto? Client);

public sealed class ServiceRequestCreateDto
{
    [Required]
    public int? ContractId { get; set; }

    [Required, StringLength(500, MinimumLength = 5)]
    public string? Description { get; set; }

    [Required, Range(typeof(decimal), "0", "10000000")]
    public decimal? CostUsd { get; set; }
}

public sealed record ExchangeRateDto(string Base, string Target, decimal Rate, DateTime RetrievedAtUtc);

public sealed record DashboardDto(
    int ClientCount,
    int DraftContracts,
    int ActiveContracts,
    int OnHoldContracts,
    int ExpiredContracts,
    int ServiceRequestCount,
    decimal TotalServiceCostZar);

public sealed class LoginRequestDto
{
    [Required]
    public string? Username { get; set; }

    [Required]
    public string? Password { get; set; }
}

public sealed record LoginResponseDto(string Token, DateTime ExpiresAtUtc, string Username, string Role, int? ClientId = null);

/// <summary>Body for POST /api/auth/register. New accounts are always Customers.</summary>
public sealed class RegisterRequestDto
{
    [Required, StringLength(50, MinimumLength = 3)]
    [RegularExpression(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Use letters, numbers, dots, dashes or underscores only.")]
    public string? Username { get; set; }

    [Required, StringLength(100, MinimumLength = 8)]
    public string? Password { get; set; }
}

public sealed record UserDto(int Id, string Username, UserRole Role, int? ClientId, string? ClientName, DateTime CreatedAtUtc);

/// <summary>Body for PATCH /api/users/{id} (Admin only).</summary>
public sealed class UserAccessUpdateDto
{
    [Required]
    public UserRole? Role { get; set; }

    /// <summary>Client company for a Customer. Ignored for Admin/Staff.</summary>
    public int? ClientId { get; set; }
}
