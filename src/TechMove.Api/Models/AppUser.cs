namespace TechMove.Api.Models;

/// <summary>
/// Admin   – full access, manages users, approves/declines contracts.
/// Staff   – day-to-day work: clients, contracts, service requests (cannot approve or delete).
/// Customer – belongs to one client company; sees only that company's data and can log service requests.
/// </summary>
public enum UserRole
{
    Admin = 0,
    Staff = 1,
    Customer = 2
}

/// <summary>A person who can sign in. Passwords are stored only as PBKDF2 hashes.</summary>
public class AppUser
{
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Customer;

    /// <summary>For customers: the client company they belong to (set by an Admin). Null until linked.</summary>
    public int? ClientId { get; set; }

    public Client? Client { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
