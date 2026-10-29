using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechMove.Api.Auth;
using TechMove.Api.Dtos;

namespace TechMove.Api.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly IUserStore _users;
    private readonly ITokenService _tokens;

    public AuthController(IUserStore users, ITokenService tokens)
    {
        _users = users;
        _tokens = tokens;
    }

    /// <summary>Exchange a username and password for a JWT bearer token.</summary>
    /// <response code="200">Credentials valid - token returned.</response>
    /// <response code="401">Wrong username or password.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<LoginResponseDto> Login(LoginRequestDto request)
    {
        var user = _users.ValidateCredentials(request.Username!, request.Password!);
        if (user is null)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Login failed", detail: "Invalid username or password.");
        }

        var token = _tokens.CreateToken(user);
        return Ok(new LoginResponseDto(token.Token, token.ExpiresAtUtc, user.Username, user.Role));
    }
}
