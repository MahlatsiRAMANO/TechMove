using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechMove.Web.Infrastructure;
using TechMove.Web.Models;
using TechMove.Web.Services.Api;

namespace TechMove.Web.Controllers;

/// <summary>Presentation only: every read and write goes through the TechMove API. Employees only.</summary>
[Authorize(Roles = UserRoles.Employees)]
public class ClientsController : Controller
{
    private readonly IClientsApi _clients;

    public ClientsController(IClientsApi clients) => _clients = clients;

    public async Task<IActionResult> Index(CancellationToken ct) => View(await _clients.GetAllAsync(ct));

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var client = await _clients.GetAsync(id, ct);
        return client is null ? NotFound() : View(client);
    }

    public IActionResult Create() => View(new Client());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,ContactDetails,Region")] Client client, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(client);

        try
        {
            var created = await _clients.CreateAsync(client, ct);
            TempData["Success"] = $"Client '{created.Name}' created.";
            return RedirectToAction(nameof(Index));
        }
        catch (ApiValidationException ex)
        {
            ModelState.AddApiErrors(ex, nameof(Client.Name), nameof(Client.ContactDetails), nameof(Client.Region));
            return View(client);
        }
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var client = await _clients.GetAsync(id, ct);
        return client is null ? NotFound() : View(client);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,ContactDetails,Region")] Client client, CancellationToken ct)
    {
        if (id != client.Id) return BadRequest();
        if (!ModelState.IsValid) return View(client);

        try
        {
            await _clients.UpdateAsync(client, ct);
            TempData["Success"] = "Client updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (ApiValidationException ex)
        {
            ModelState.AddApiErrors(ex, nameof(Client.Name), nameof(Client.ContactDetails), nameof(Client.Region));
            return View(client);
        }
    }

    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var client = await _clients.GetAsync(id, ct);
        return client is null ? NotFound() : View(client);
    }

    [HttpPost, ActionName("Delete")]
    [Authorize(Roles = UserRoles.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken ct)
    {
        try
        {
            await _clients.DeleteAsync(id, ct);
            TempData["Success"] = "Client deleted.";
        }
        catch (ApiValidationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}
