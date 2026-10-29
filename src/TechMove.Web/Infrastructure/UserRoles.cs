using System.Security.Claims;

namespace TechMove.Web.Infrastructure;

/// <summary>Role names (must match the API) and helpers used by controllers and views to show/hide actions.
/// The API enforces the same rules, so hiding a button is a convenience, not the security boundary.</summary>
public static class UserRoles
{
    public const string Admin = "Admin";
    public const string Staff = "Staff";
    public const string Customer = "Customer";
    public const string Employees = Admin + "," + Staff;

    public const string ClientIdClaim = "client_id";

    public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole(Admin);

    public static bool IsEmployee(this ClaimsPrincipal user) => user.IsInRole(Admin) || user.IsInRole(Staff);

    public static bool IsCustomer(this ClaimsPrincipal user) => user.IsInRole(Customer);

    /// <summary>A customer who has signed up but has not yet been linked to a client by an Admin.</summary>
    public static bool IsUnlinkedCustomer(this ClaimsPrincipal user) =>
        user.IsCustomer() && string.IsNullOrEmpty(user.FindFirst(ClientIdClaim)?.Value);
}
