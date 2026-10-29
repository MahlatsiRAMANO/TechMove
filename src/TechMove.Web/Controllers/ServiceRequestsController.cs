using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using TechMove.Web.Infrastructure;
using TechMove.Web.Models;
using TechMove.Web.Services.Api;
using TechMove.Web.ViewModels;

namespace TechMove.Web.Controllers;

/// <summary>Presentation only: the API checks the workflow, fetches the exchange rate and saves the request.</summary>
public class ServiceRequestsController : Controller
{
    private readonly IServiceRequestsApi _requests;
    private readonly IContractsApi _contracts;

    public ServiceRequestsController(IServiceRequestsApi requests, IContractsApi contracts)
    {
        _requests = requests;
        _contracts = contracts;
    }

    public async Task<IActionResult> Index(CancellationToken ct) => View(await _requests.GetAllAsync(ct));

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var request = await _requests.GetAsync(id, ct);
        return request is null ? NotFound() : View(request);
    }

    public async Task<IActionResult> Create(int? contractId, CancellationToken ct)
    {
        var vm = new ServiceRequestCreateViewModel { ContractId = contractId };
        await PopulateContractsAsync(vm, ct);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ServiceRequestCreateViewModel vm, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var created = await _requests.CreateAsync(vm.ContractId!.Value, vm.Description!, vm.CostUsd!.Value, ct);
                TempData["Success"] =
                    $"Service request created: {created.CostUsd:N2} USD × {created.ExchangeRate:N4} = R {created.Cost:N2}.";
                return RedirectToAction(nameof(Details), new { id = created.Id });
            }
            catch (ApiValidationException ex)
            {
                // e.g. "Service requests cannot be created for a contract that is 'Expired'."
                ModelState.AddModelError(nameof(vm.ContractId), ex.Message);
            }
            catch (ApiNotFoundException ex)
            {
                ModelState.AddModelError(nameof(vm.ContractId), ex.Message);
            }
            catch (ApiUnavailableException ex)
            {
                // 503 from the API when the currency service is down, or the API itself is unreachable.
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }

        await PopulateContractsAsync(vm, ct);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = UserRoles.Employees)]
    public async Task<IActionResult> UpdateStatus(int id, ServiceRequestStatus status, CancellationToken ct)
    {
        try
        {
            await _requests.ChangeStatusAsync(id, status, ct);
            TempData["Success"] = "Status updated.";
        }
        catch (ApiValidationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>Used by the Create page's live ZAR preview (wwwroot/js/service-request.js).</summary>
    [HttpGet]
    public async Task<IActionResult> ExchangeRate(CancellationToken ct)
    {
        try
        {
            var rate = await _requests.GetUsdToZarRateAsync(ct);
            return Json(new { success = true, rate = rate.Rate, retrievedAtUtc = rate.RetrievedAtUtc, error = (string?)null });
        }
        catch (ApiException ex) when (ex is not ApiUnauthorizedException)
        {
            return Json(new { success = false, rate = 0m, retrievedAtUtc = (DateTime?)null, error = ex.Message });
        }
    }

    private async Task PopulateContractsAsync(ServiceRequestCreateViewModel vm, CancellationToken ct)
    {
        var open = await _contracts.GetOpenAsync(ct);
        vm.Contracts = open.Select(c => new SelectListItem(
            $"#{c.Id} – {c.Client?.Name} ({c.Status}, {c.ServiceLevel}, ends {c.EndDate:yyyy-MM-dd})",
            c.Id.ToString(),
            c.Id == vm.ContractId));
    }
}
