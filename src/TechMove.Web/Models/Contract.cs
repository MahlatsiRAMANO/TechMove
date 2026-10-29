using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace TechMove.Web.Models;

/// <summary>A service contract between TechMove and a client (as returned by the TechMove API).</summary>
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


    public DateTime? SignedAgreementUploadedAtUtc { get; set; }

    [ValidateNever]
    public ICollection<ServiceRequest> ServiceRequests { get; set; } = new List<ServiceRequest>();

    public bool HasSignedAgreement { get; set; }

    /// <summary>Calculated by the API's workflow - the frontend does not re-implement business rules.</summary>
    public bool CanCreateServiceRequest { get; set; }

    /// <summary>Statuses the API's workflow allows from the current one (includes the current status).</summary>
    public List<ContractStatus> AllowedNextStatuses { get; set; } = new();
}
