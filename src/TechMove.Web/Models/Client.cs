using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace TechMove.Web.Models;

/// <summary>A TechMove customer. One client has many contracts.</summary>
public class Client
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Client name is required.")]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Contact details are required.")]
    [StringLength(200)]
    [Display(Name = "Contact Details")]
    public string ContactDetails { get; set; } = string.Empty;

    [Required(ErrorMessage = "Region is required.")]
    [StringLength(50)]
    public string Region { get; set; } = string.Empty;

    [ValidateNever]
    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
}
