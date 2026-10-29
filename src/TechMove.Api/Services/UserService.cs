using System.Text.RegularExpressions;
using TechMove.Api.Auth;
using TechMove.Api.Data.Repositories;
using TechMove.Api.Models;

namespace TechMove.Api.Services;

/// <summary>Raised when something already exists (e.g. a username is taken). Mapped to HTTP 409.</summary>
public class ConflictException : TechMoveException
{
    public ConflictException(string message) : base(message) { }
}

public interface IUserService
{
    Task<AppUser?> ValidateCredentialsAsync(string username, string password, CancellationToken ct = default);
    Task<AppUser> RegisterAsync(string username, string password, CancellationToken ct = default);
    Task<IReadOnlyList<AppUser>> GetAllAsync(CancellationToken ct = default);
    Task<AppUser?> GetAsync(int id, CancellationToken ct = default);
    Task<AppUser> UpdateAccessAsync(int id, UserRole role, int? clientId, int actingUserId, CancellationToken ct = default);
    Task DeleteAsync(int id, int actingUserId, CancellationToken ct = default);
}

/// <summary>
/// Account rules: sign-up always creates a Customer (nobody can make themselves an Admin);
/// only Admins change roles; customers must be linked to an existing client; an Admin cannot
/// demote or delete themselves, and the last Admin can never be removed.
/// </summary>
public sealed partial class UserService : IUserService
{
    private readonly IUserRepository _users;
    private readonly IClientRepository _clients;
    private readonly IDateTimeProvider _clock;

    public UserService(IUserRepository users, IClientRepository clients, IDateTimeProvider clock)
    {
        _users = users;
        _clients = clients;
        _clock = clock;
    }

    public async Task<AppUser?> ValidateCredentialsAsync(string username, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password)) return null;

        var user = await _users.GetByUsernameAsync(username, ct);
        return user is not null && PasswordHasher.Verify(password, user.PasswordHash) ? user : null;
    }

    public async Task<AppUser> RegisterAsync(string username, string password, CancellationToken ct = default)
    {
        var name = (username ?? string.Empty).Trim();
        if (!UsernamePattern().IsMatch(name))
        {
            throw new BusinessRuleException("Username must be 3–50 characters: letters, numbers, dots, dashes or underscores.");
        }

        EnsureStrongPassword(password);

        if (await _users.GetByUsernameAsync(name, ct) is not null)
        {
            throw new ConflictException($"The username '{name}' is already taken.");
        }

        var user = new AppUser
        {
            Username = name,
            PasswordHash = PasswordHasher.Hash(password),
            Role = UserRole.Customer,
            CreatedAtUtc = _clock.UtcNow
        };

        await _users.AddAsync(user, ct);
        await _users.SaveChangesAsync(ct);
        return user;
    }

    public Task<IReadOnlyList<AppUser>> GetAllAsync(CancellationToken ct = default) => _users.GetAllWithClientAsync(ct);

    public Task<AppUser?> GetAsync(int id, CancellationToken ct = default) => _users.GetByIdAsync(id, ct);

    public async Task<AppUser> UpdateAccessAsync(int id, UserRole role, int? clientId, int actingUserId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(id, ct) ?? throw new NotFoundException($"User {id} was not found.");

        if (user.Id == actingUserId && role != user.Role)
        {
            throw new BusinessRuleException("You cannot change your own role.");
        }

        if (user.Role == UserRole.Admin && role != UserRole.Admin && await _users.CountByRoleAsync(UserRole.Admin, ct) <= 1)
        {
            throw new BusinessRuleException("There must always be at least one Admin.");
        }

        if (role == UserRole.Customer)
        {
            if (clientId.HasValue && await _clients.GetByIdAsync(clientId.Value, ct) is null)
            {
                throw new BusinessRuleException("The selected client does not exist.");
            }
        }
        else
        {
            clientId = null; // only customers belong to a client
        }

        user.Role = role;
        user.ClientId = clientId;
        await _users.SaveChangesAsync(ct);
        return user;
    }

    public async Task DeleteAsync(int id, int actingUserId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(id, ct) ?? throw new NotFoundException($"User {id} was not found.");

        if (user.Id == actingUserId)
        {
            throw new BusinessRuleException("You cannot delete your own account.");
        }

        if (user.Role == UserRole.Admin && await _users.CountByRoleAsync(UserRole.Admin, ct) <= 1)
        {
            throw new BusinessRuleException("There must always be at least one Admin.");
        }

        _users.Remove(user);
        await _users.SaveChangesAsync(ct);
    }

    internal static void EnsureStrongPassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8 ||
            !password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit))
        {
            throw new BusinessRuleException("Password must be at least 8 characters and include an upper-case letter, a lower-case letter and a number.");
        }
    }

    [GeneratedRegex(@"^[A-Za-z0-9._-]{3,50}$")]
    private static partial Regex UsernamePattern();
}
