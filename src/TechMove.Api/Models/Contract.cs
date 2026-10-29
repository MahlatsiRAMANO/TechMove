using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace TechMove.Api.Models;

/// <summary>A service contract between TechMove and a client.</summary>
public class Contract
{
    public int Id { get; set; }

    [Display(Name = "Client")]
    public int ClientId { get; set; }

    [ValidateNever]
    public Client? Client { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Start Date")]
    public DateTime StartDate { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "End Date")]
    public DateTime EndDate { get; set; }

    public ContractStatus Status { get; set; } = ContractStatus.Draft;

    [Display(Name = "Service Level")]
    public ServiceLevel ServiceLevel { get; set; } = ServiceLevel.Bronze;

    /// <summary>The name the user uploaded (shown in the UI and used for downloads).</summary>
    [Display(Name = "Signed Agreement")]
    public string? SignedAgreementFileName { get; set; }

    /// <summary>The GUID-based name on the file server (prevents overwrites and path tricks).</summary>
    public string? SignedAgreementStoredName { get; set; }

    public DateTime? SignedAgreementUploadedAtUtc { get; set; }

    [ValidateNever]
    public ICollection<ServiceRequest> ServiceRequests { get; set; } = new List<ServiceRequest>();

    public bool HasSignedAgreement => !string.IsNullOrEmpty(SignedAgreementStoredName);
}
