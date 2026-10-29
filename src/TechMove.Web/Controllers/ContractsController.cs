using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using TechMove.Web.Infrastructure;
using TechMove.Web.Models;
using TechMove.Web.Services.Api;
using TechMove.Web.ViewModels;

namespace TechMove.Web.Controllers;

/// <summary>Presentation only: calls the TechMove API for every read and write. No SQL, no business rules.</summary>
public class ContractsController : Controller
{
    private const long UploadLimit = 11 * 1024 * 1024;
    private static readonly string[] FormFields =
        { nameof(ContractFormViewModel.ClientId), nameof(ContractFormViewModel.StartDate), nameof(ContractFormViewModel.EndDate),
          nameof(ContractFormViewModel.Status), nameof(ContractFormViewModel.ServiceLevel), nameof(ContractFormViewModel.SignedAgreement) };

    private readonly IContractsApi _contracts;
    private readonly IClientsApi _clients;

    public ContractsController(IContractsApi contracts, IClientsApi clients)
    {
        _contracts = contracts;
        _clients = clients;
    }

    // GET /Contracts?FromDate=..&ToDate=..&Status=..&ClientName=..  →  GET /api/contracts?...
    public async Task<IActionResult> Index([FromQuery] ContractSearchCriteria criteria, CancellationToken ct) =>
        View(new ContractIndexViewModel { Criteria = criteria, Contracts = await _contracts.SearchAsync(criteria, ct) });

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var contract = await _contracts.GetAsync(id, ct);
        return contract is null ? NotFound() : View(contract);
    }

    [Authorize(Roles = UserRoles.Employees)]
    public async Task<IActionResult> Create(int? clientId, CancellationToken ct)
    {
        var vm = new ContractFormViewModel
        {
            ClientId = clientId,
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddYears(1)
        };
        await PopulateListsAsync(vm, ct);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = UserRoles.Employees)]
    [RequestSizeLimit(UploadLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadLimit)]
    public async Task<IActionResult> Create(ContractFormViewModel vm, CancellationToken ct)
    {
        if (vm.SignedAgreement is null)
        {
            ModelState.AddModelError(nameof(vm.SignedAgreement), "A signed agreement PDF is required for every contract.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                var created = await _contracts.CreateAsync(vm.ToEntity(), vm.SignedAgreement!, ct);
                TempData["Success"] = $"Contract #{created.Id} created.";
                return RedirectToAction(nameof(Details), new { id = created.Id });
            }
            catch (ApiValidationException ex)
            {
                ModelState.AddApiErrors(ex, FormFields);
            }
        }

        await PopulateListsAsync(vm, ct);
        return View(vm);
    }

    [Authorize(Roles = UserRoles.Employees)]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var contract = await _contracts.GetAsync(id, ct);
        if (contract is null) return NotFound();

        var vm = ContractFormViewModel.FromEntity(contract);
        await PopulateListsAsync(vm, ct);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = UserRoles.Employees)]
    public async Task<IActionResult> Edit(int id, ContractFormViewModel vm, CancellationToken ct)
    {
        if (id != vm.Id) return BadRequest();

        if (ModelState.IsValid)
        {
            try
            {
                await _contracts.UpdateAsync(vm.ToEntity(), ct);
                TempData["Success"] = "Contract updated.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (ApiValidationException ex)
            {
                ModelState.AddApiErrors(ex, FormFields);
            }
        }

        vm.ExistingAgreementName = (await _contracts.GetAsync(id, ct))?.SignedAgreementFileName;
        await PopulateListsAsync(vm, ct);
        return View(vm);
    }

    /// <summary>Approve / decline buttons on the details page → PATCH /api/contracts/{id}/status (Admin only).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> ChangeStatus(int id, ContractStatus status, CancellationToken ct)
    {
        try
        {
            var updated = await _contracts.ChangeStatusAsync(id, status, ct);
            TempData["Success"] = $"Contract #{id} is now {Label(updated.Status)}.";
        }
        catch (ApiValidationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (ApiUnauthorizedException ex) when (ex.IsForbidden)
        {
            TempData["Error"] = "Only administrators can approve or decline contracts.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = UserRoles.Employees)]
    [RequestSizeLimit(UploadLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadLimit)]
    public async Task<IActionResult> UploadAgreement(int id, IFormFile? signedAgreement, CancellationToken ct)
    {
        if (signedAgreement is null)
        {
            TempData["Error"] = "Please choose a PDF file to upload.";
            return RedirectToAction(nameof(Details), new { id });
        }

        try
        {
            await _contracts.UploadAgreementAsync(id, signedAgreement, ct);
            TempData["Success"] = "Signed agreement uploaded.";
        }
        catch (ApiValidationException ex)
        {
            TempData["Error"] = ex.Errors.Values.SelectMany(e => e).FirstOrDefault() ?? ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> DownloadAgreement(int id, CancellationToken ct)
    {
        try
        {
            var file = await _contracts.DownloadAgreementAsync(id, ct);
            HttpContext.Response.RegisterForDispose(file);
            return File(file.Content, file.ContentType, file.FileName);
        }
        catch (ApiNotFoundException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    private async Task PopulateListsAsync(ContractFormViewModel vm, CancellationToken ct)
    {
        var clients = await _clients.GetAllAsync(ct);
        vm.Clients = clients.Select(c => new SelectListItem($"{c.Name} ({c.Region})", c.Id.ToString()));

        // New contracts start as Draft or Active; later changes go through the workflow (PATCH /status).
        vm.StatusOptions = new[] { ContractStatus.Draft, ContractStatus.Active }
            .Select(s => new SelectListItem(Label(s), ((int)s).ToString(), s == vm.Status));
    }

    private static string Label(ContractStatus s) => s == ContractStatus.OnHold ? "On Hold" : s.ToString();
}
