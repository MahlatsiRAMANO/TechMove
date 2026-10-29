using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechMove.Api.Auth;
using TechMove.Api.Dtos;
using TechMove.Api.Services;

namespace TechMove.Api.Controllers;

/// <summary>User administration - Admin only.</summary>
[ApiController]
[Route("api/users")]
[Authorize(Roles = Roles.Admin)]
[Produces("application/json")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserService _users;
    private readonly ICurrentUser _currentUser;

    public UsersController(IUserService users, ICurrentUser currentUser)
    {
        _users = users;
        _currentUser = currentUser;
    }

    /// <summary>List every account with its role and client.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetAll(CancellationToken ct) =>
        Ok((await _users.GetAllAsync(ct)).Select(u => DtoMapper.ToUserDto(u)));

    /// <summary>Change a user's role and (for customers) the client company they belong to.</summary>
    /// <remarks>Body: { "role": "Customer", "clientId": 2 }  or  { "role": "Staff" }</remarks>
    [HttpPatch("{id:int}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> UpdateAccess(int id, UserAccessUpdateDto dto, CancellationToken ct)
    {
        await _users.UpdateAccessAsync(id, dto.Role!.Value, dto.ClientId, _currentUser.UserId, ct);
        var updated = (await _users.GetAllAsync(ct)).First(u => u.Id == id);
        return Ok(DtoMapper.ToUserDto(updated));
    }

    /// <summary>Delete an account (you cannot delete yourself or the last Admin).</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _users.DeleteAsync(id, _currentUser.UserId, ct);
        return NoContent();
    }
}
