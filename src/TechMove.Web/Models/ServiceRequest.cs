using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace TechMove.Web.Models;

/// <summary>Work requested under a contract. Cost is captured in USD and stored in ZAR.</summary>
public class ServiceRequest
{
    public int Id { get; set; }

    [Display(Name = "Contract")]
    public int ContractId { get; set; }

    [ValidateNever]
    public Contract? Contract { get; set; }

    [Required]
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Cost (USD)")]
    [DataType(DataType.Currency)]
    public decimal CostUsd { get; set; }

    [Display(Name = "USD→ZAR Rate")]
    public decimal ExchangeRate { get; set; }

    /// <summary>The local (ZAR) cost - this is what TechMove reports on.</summary>
    [Display(Name = "Cost (ZAR)")]
    public decimal Cost { get; set; }

    public ServiceRequestStatus Status { get; set; } = ServiceRequestStatus.Pending;

    [Display(Name = "Created (UTC)")]
    public DateTime CreatedAtUtc { get; set; }
}
