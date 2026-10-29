using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechMove.Api.Auth;
using TechMove.Api.Dtos;
using TechMove.Api.Models;
using TechMove.Api.Services;

namespace TechMove.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly IUserService _users;
    private readonly ITokenService _tokens;
    private readonly ICurrentUser _currentUser;

    public AuthController(IUserService users, ITokenService tokens, ICurrentUser currentUser)
    {
        _users = users;
        _tokens = tokens;
        _currentUser = currentUser;
    }

    /// <summary>Exchange a username and password for a JWT bearer token.</summary>
    /// <response code="200">Credentials valid - token returned.</response>
    /// <response code="401">Wrong username or password.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponseDto>> Login(LoginRequestDto request, CancellationToken ct)
    {
        var user = await _users.ValidateCredentialsAsync(request.Username!, request.Password!, ct);
        if (user is null)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Login failed", detail: "Invalid username or password.");
        }

        return Ok(IssueToken(user));
    }

    /// <summary>Create a new account. New accounts are Customers until an Admin links them to a client or changes their role.</summary>
    /// <response code="201">Account created and signed in - token returned.</response>
    /// <response code="400">Invalid username or weak password.</response>
    /// <response code="409">Username already taken.</response>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LoginResponseDto>> Register(RegisterRequestDto request, CancellationToken ct)
    {
        var user = await _users.RegisterAsync(request.Username!, request.Password!, ct);
        return Created("/api/auth/me", IssueToken(user));
    }

    /// <summary>The signed-in user's profile.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct)
    {
        var user = await _users.GetAsync(_currentUser.UserId, ct);
        return user is null ? Unauthorized() : Ok(DtoMapper.ToUserDto(user));
    }

    private LoginResponseDto IssueToken(AppUser user)
    {
        var token = _tokens.CreateToken(user);
        return new LoginResponseDto(token.Token, token.ExpiresAtUtc, user.Username, user.Role.ToString(), user.ClientId);
    }
}
