using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TechMove.Api.Models;

namespace TechMove.Api.Auth;

/// <summary>Role names used in [Authorize(Roles = ...)] attributes.</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Staff = "Staff";
    public const string Customer = "Customer";

    /// <summary>Internal TechMove employees.</summary>
    public const string Employees = Admin + "," + Staff;
}

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "TechMove.Api";
    public string Audience { get; set; } = "TechMove.Web";

    /// <summary>HMAC signing key - at least 32 characters. Supplied by configuration / environment variables.</summary>
    public string Key { get; set; } = string.Empty;

    public int ExpiryMinutes { get; set; } = 120;
}

/// <summary>PBKDF2-SHA256 password hashing with a random salt and constant-time comparison.</summary>
public static class PasswordHasher
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('.', 3);
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations))
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[1]);
            var expected = Convert.FromBase64String(parts[2]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public sealed record IssuedToken(string Token, DateTime ExpiresAtUtc);

public interface ITokenService
{
    IssuedToken CreateToken(AppUser user);
}

/// <summary>Issues signed JWTs (HMAC-SHA256) containing the user's name and role.</summary>
public sealed class JwtTokenService : ITokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options) => _options = options.Value;

    public IssuedToken CreateToken(AppUser user)
    {
        var expires = DateTime.UtcNow.AddMinutes(_options.ExpiryMinutes);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Expires = expires,
            Subject = new ClaimsIdentity(Claims(user)),
            SigningCredentials = new SigningCredentials(SigningKey(_options.Key), SecurityAlgorithms.HmacSha256)
        };

        return new IssuedToken(new JsonWebTokenHandler().CreateToken(descriptor), expires);
    }

    public static SymmetricSecurityKey SigningKey(string key) => new(Encoding.UTF8.GetBytes(key));

    private static IEnumerable<Claim> Claims(AppUser user)
    {
        yield return new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString());
        yield return new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"));
        yield return new Claim("name", user.Username);
        yield return new Claim("role", user.Role.ToString());
        if (user.ClientId.HasValue)
        {
            yield return new Claim(HttpCurrentUser.ClientIdClaim, user.ClientId.Value.ToString());
        }
    }
}
