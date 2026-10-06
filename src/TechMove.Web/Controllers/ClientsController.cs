using Microsoft.AspNetCore.Mvc;
using TechMove.Web.Models;
using TechMove.Web.Services;

namespace TechMove.Web.Controllers;

public class ClientsController : Controller
{
    private readonly IClientService _clients;

    public ClientsController(IClientService clients) => _clients = clients;

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

        await _clients.CreateAsync(client, ct);
        TempData["Success"] = $"Client '{client.Name}' created.";
        return RedirectToAction(nameof(Index));
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
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        TempData["Success"] = "Client updated.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var client = await _clients.GetAsync(id, ct);
        return client is null ? NotFound() : View(client);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken ct)
    {
        try
        {
            await _clients.DeleteAsync(id, ct);
            TempData["Success"] = "Client deleted.";
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (BusinessRuleException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}
