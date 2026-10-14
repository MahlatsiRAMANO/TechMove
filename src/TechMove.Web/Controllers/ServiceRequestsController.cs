using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using TechMove.Web.Models;
using TechMove.Web.Services;
using TechMove.Web.Services.Currency;
using TechMove.Web.ViewModels;

namespace TechMove.Web.Controllers;

public class ServiceRequestsController : Controller
{
    private readonly IServiceRequestService _requests;
    private readonly IContractService _contracts;
    private readonly IExchangeRateProvider _rates;

    public ServiceRequestsController(IServiceRequestService requests, IContractService contracts, IExchangeRateProvider rates)
    {
        _requests = requests;
        _contracts = contracts;
        _rates = rates;
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
                var created = await _requests.CreateAsync(vm.ContractId!.Value, vm.Description, vm.CostUsd!.Value, ct);
                TempData["Success"] =
                    $"Service request created: {created.CostUsd:N2} USD × {created.ExchangeRate:N4} = R {created.Cost:N2}.";
                return RedirectToAction(nameof(Details), new { id = created.Id });
            }
            catch (NotFoundException ex)
            {
                ModelState.AddModelError(nameof(vm.ContractId), ex.Message);
            }
            catch (BusinessRuleException ex)
            {
                ModelState.AddModelError(nameof(vm.ContractId), ex.Message);
            }
            catch (ExternalServiceException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }

        await PopulateContractsAsync(vm, ct);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, ServiceRequestStatus status, CancellationToken ct)
    {
        try
        {
            await _requests.UpdateStatusAsync(id, status, ct);
            TempData["Success"] = "Status updated.";
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (BusinessRuleException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> ExchangeRate(CancellationToken ct)
    {
        var rate = await _rates.GetUsdToZarRateAsync(ct);
        return Json(new { success = rate.Success, rate = rate.Rate, retrievedAtUtc = rate.RetrievedAtUtc, error = rate.Error });
    }

    private async Task PopulateContractsAsync(ServiceRequestCreateViewModel vm, CancellationToken ct)
    {
        var open = await _contracts.GetOpenForServiceRequestsAsync(ct);
        vm.Contracts = open.Select(c => new SelectListItem(
            $"#{c.Id} – {c.Client?.Name} ({c.Status}, {c.ServiceLevel}, ends {c.EndDate:yyyy-MM-dd})",
            c.Id.ToString(),
            c.Id == vm.ContractId));
    }
}
