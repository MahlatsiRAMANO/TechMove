using System.ComponentModel.DataAnnotations;

namespace TechMove.Api.Models;

/// <summary>Filter values used by the admin contract search.</summary>
public class ContractSearchCriteria
{
    [DataType(DataType.Date)]
    [Display(Name = "From")]
    public DateTime? FromDate { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "To")]
    public DateTime? ToDate { get; set; }

    public ContractStatus? Status { get; set; }

    [Display(Name = "Client name")]
    [StringLength(100)]
    public string? ClientName { get; set; }

    /// <summary>Restricts results to one client. Customers are always forced to their own client.</summary>
    public int? ClientId { get; set; }

    public bool IsEmpty =>
        FromDate is null && ToDate is null && Status is null && string.IsNullOrWhiteSpace(ClientName);
}
