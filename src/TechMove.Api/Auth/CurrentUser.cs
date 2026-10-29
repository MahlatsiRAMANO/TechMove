using System.Security.Claims;
using TechMove.Api.Models;

namespace TechMove.Api.Auth;

/// <summary>The signed-in caller, read from the validated JWT.</summary>
public interface ICurrentUser
{
    int UserId { get; }
    string Username { get; }
    UserRole Role { get; }
    int? ClientId { get; }

    bool IsAdmin { get; }
    bool IsCustomer { get; }

    /// <summary>
    /// Data-scope for queries: null = may see every client (Admin/Staff);
    /// otherwise the only client whose data may be returned. A customer not yet linked
    /// to a client gets -1, which matches nothing.
    /// </summary>
    int? ClientScope { get; }

    /// <summary>True when the caller may see data belonging to the given client.</summary>
    bool CanAccessClient(int clientId);
}

public sealed class HttpCurrentUser : ICurrentUser
{
    public const string ClientIdClaim = "client_id";

    private readonly IHttpContextAccessor _accessor;

    public HttpCurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal Principal => _accessor.HttpContext?.User ?? new ClaimsPrincipal();

    public int UserId => int.TryParse(Principal.FindFirstValue("sub"), out var id) ? id : 0;

    public string Username => Principal.FindFirstValue("name") ?? string.Empty;

    public UserRole Role => Enum.TryParse<UserRole>(Principal.FindFirstValue("role"), out var role) ? role : UserRole.Customer;

    public int? ClientId => int.TryParse(Principal.FindFirstValue(ClientIdClaim), out var id) ? id : null;

    public bool IsAdmin => Role == UserRole.Admin;

    public bool IsCustomer => Role == UserRole.Customer;

    public int? ClientScope => IsCustomer ? ClientId ?? -1 : null;

    public bool CanAccessClient(int clientId) => ClientScope is null || ClientScope == clientId;
}
