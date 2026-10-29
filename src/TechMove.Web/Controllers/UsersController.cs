using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechMove.Web.Infrastructure;
using TechMove.Web.Services.Api;
using TechMove.Web.ViewModels;

namespace TechMove.Web.Controllers;

/// <summary>Admin-only page: change roles, link customers to their company, delete accounts.</summary>
[Authorize(Roles = UserRoles.Admin)]
public class UsersController : Controller
{
    private readonly IUsersApi _users;
    private readonly IClientsApi _clients;

    public UsersController(IUsersApi users, IClientsApi clients)
    {
        _users = users;
        _clients = clients;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        // Sequential: both go through the same signed-in HttpContext.
        var users = await _users.GetAllAsync(ct);
        var clients = await _clients.GetAllAsync(ct);
        return View(new UsersIndexViewModel { Users = users, Clients = clients });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAccess(int id, string role, int? clientId, CancellationToken ct)
    {
        try
        {
            var updated = await _users.UpdateAccessAsync(id, role, clientId, ct);
            TempData["Success"] = updated.Role == UserRoles.Customer && updated.ClientName is not null
                ? $"{updated.Username} is now a Customer of {updated.ClientName}. They must sign out and in again to see the change."
                : $"{updated.Username} is now {updated.Role}. They must sign out and in again to see the change.";
        }
        catch (ApiValidationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            await _users.DeleteAsync(id, ct);
            TempData["Success"] = "Account deleted.";
        }
        catch (ApiValidationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}
