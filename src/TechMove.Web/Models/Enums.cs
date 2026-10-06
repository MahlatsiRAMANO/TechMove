using System.ComponentModel.DataAnnotations;

namespace TechMove.Web.Models;

public enum ContractStatus
{
    Draft = 0,
    Active = 1,
    Expired = 2,

    [Display(Name = "On Hold")]
    OnHold = 3
}

public enum ServiceLevel
{
    Bronze = 0,
    Silver = 1,
    Gold = 2,
    Platinum = 3
}

public enum ServiceRequestStatus
{
    Pending = 0,

    [Display(Name = "In Progress")]
    InProgress = 1,

    Completed = 2,
    Cancelled = 3
}
