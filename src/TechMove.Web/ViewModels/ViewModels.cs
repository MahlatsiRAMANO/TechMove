using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using TechMove.Web.Models;

namespace TechMove.Web.ViewModels;

public class DashboardViewModel
{
    public int ClientCount { get; set; }
    public int ActiveContracts { get; set; }
    public int DraftContracts { get; set; }
    public int OnHoldContracts { get; set; }
    public int ExpiredContracts { get; set; }
    public int ServiceRequestCount { get; set; }
    public decimal TotalServiceCostZar { get; set; }
}

public class ContractFormViewModel : IValidatableObject
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Please select a client.")]
    [Display(Name = "Client")]
    public int? ClientId { get; set; }

    [Required(ErrorMessage = "Start date is required.")]
    [DataType(DataType.Date)]
    [Display(Name = "Start Date")]
    public DateTime? StartDate { get; set; }

    [Required(ErrorMessage = "End date is required.")]
    [DataType(DataType.Date)]
    [Display(Name = "End Date")]
    public DateTime? EndDate { get; set; }

    [Required]
    public ContractStatus Status { get; set; } = ContractStatus.Draft;

    [Required]
    [Display(Name = "Service Level")]
    public ServiceLevel ServiceLevel { get; set; } = ServiceLevel.Bronze;

    /// <summary>Required on create (checked in the controller/service); not used on edit.</summary>
    [Display(Name = "Signed Agreement (PDF)")]
    [ValidateNever]
    public IFormFile? SignedAgreement { get; set; }

    [ValidateNever]
    public string? ExistingAgreementName { get; set; }

    [ValidateNever]
    public IEnumerable<SelectListItem> Clients { get; set; } = Enumerable.Empty<SelectListItem>();

    [ValidateNever]
    public IEnumerable<SelectListItem> StatusOptions { get; set; } = Enumerable.Empty<SelectListItem>();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartDate.HasValue && EndDate.HasValue && EndDate.Value.Date <= StartDate.Value.Date)
        {
            yield return new ValidationResult("End date must be after the start date.", new[] { nameof(EndDate) });
        }
    }

    public Contract ToEntity() => new()
    {
        Id = Id,
        ClientId = ClientId ?? 0,
        StartDate = StartDate?.Date ?? default,
        EndDate = EndDate?.Date ?? default,
        Status = Status,
        ServiceLevel = ServiceLevel
    };

    public static ContractFormViewModel FromEntity(Contract c) => new()
    {
        Id = c.Id,
        ClientId = c.ClientId,
        StartDate = c.StartDate,
        EndDate = c.EndDate,
        Status = c.Status,
        ServiceLevel = c.ServiceLevel,
        ExistingAgreementName = c.SignedAgreementFileName
    };
}

public class ContractIndexViewModel
{
    public ContractSearchCriteria Criteria { get; set; } = new();
    public IReadOnlyList<Contract> Contracts { get; set; } = Array.Empty<Contract>();
}

public class ServiceRequestCreateViewModel
{
    [Required(ErrorMessage = "Please select a contract.")]
    [Display(Name = "Contract")]
    public int? ContractId { get; set; }

    [Required(ErrorMessage = "A description is required.")]
    [StringLength(500, MinimumLength = 5, ErrorMessage = "Description must be between 5 and 500 characters.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Enter the cost in US dollars.")]
    [Range(typeof(decimal), "0", "10000000", ErrorMessage = "Cost must be between 0 and 10,000,000 USD.")]
    [Display(Name = "Cost (USD)")]
    public decimal? CostUsd { get; set; }

    [ValidateNever]
    public IEnumerable<SelectListItem> Contracts { get; set; } = Enumerable.Empty<SelectListItem>();
}
