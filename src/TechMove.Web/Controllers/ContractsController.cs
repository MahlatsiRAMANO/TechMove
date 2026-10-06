using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using TechMove.Web.Models;
using TechMove.Web.Services;
using TechMove.Web.Services.Files;
using TechMove.Web.Services.Workflow;
using TechMove.Web.ViewModels;

namespace TechMove.Web.Controllers;

/// <summary>Thin controller: it maps HTTP to the service layer and shows errors. No business rules here.</summary>
public class ContractsController : Controller
{
    private const long UploadLimit = PdfFileValidator.MaxFileSizeBytes + 1024 * 1024;

    private readonly IContractService _contracts;
    private readonly IClientService _clients;
    private readonly IContractWorkflow _workflow;

    public ContractsController(IContractService contracts, IClientService clients, IContractWorkflow workflow)
    {
        _contracts = contracts;
        _clients = clients;
        _workflow = workflow;
    }

    // GET /Contracts?FromDate=..&ToDate=..&Status=..&ClientName=..
    public async Task<IActionResult> Index([FromQuery] ContractSearchCriteria criteria, CancellationToken ct)
    {
        var vm = new ContractIndexViewModel
        {
            Criteria = criteria,
            Contracts = await _contracts.SearchAsync(criteria, ct)
        };
        return View(vm);
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var contract = await _contracts.GetDetailsAsync(id, ct);
        if (contract is null) return NotFound();

        ViewBag.CanRequestService = _workflow.CanCreateServiceRequest(contract, DateTime.Today);
        return View(contract);
    }

    public async Task<IActionResult> Create(int? clientId, CancellationToken ct)
    {
        var vm = new ContractFormViewModel
        {
            ClientId = clientId,
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddYears(1)
        };
        await PopulateListsAsync(vm, isCreate: true, ct);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
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
                var created = await _contracts.CreateAsync(vm.ToEntity(), vm.SignedAgreement, ct);
                TempData["Success"] = $"Contract #{created.Id} created.";
                return RedirectToAction(nameof(Details), new { id = created.Id });
            }
            catch (InvalidFileException ex)
            {
                ModelState.AddModelError(nameof(vm.SignedAgreement), ex.Message);
            }
            catch (BusinessRuleException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }

        await PopulateListsAsync(vm, isCreate: true, ct);
        return View(vm);
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var contract = await _contracts.GetDetailsAsync(id, ct);
        if (contract is null) return NotFound();

        var vm = ContractFormViewModel.FromEntity(contract);
        await PopulateListsAsync(vm, isCreate: false, ct, contract.Status);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
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
            catch (NotFoundException)
            {
                return NotFound();
            }
            catch (BusinessRuleException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }

        var current = await _contracts.GetDetailsAsync(id, ct);
        vm.ExistingAgreementName = current?.SignedAgreementFileName;
        await PopulateListsAsync(vm, isCreate: false, ct, current?.Status ?? vm.Status);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(UploadLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadLimit)]
    public async Task<IActionResult> UploadAgreement(int id, IFormFile? signedAgreement, CancellationToken ct)
    {
        try
        {
            await _contracts.UploadAgreementAsync(id, signedAgreement, ct);
            TempData["Success"] = "Signed agreement uploaded.";
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (InvalidFileException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> DownloadAgreement(int id, CancellationToken ct)
    {
        try
        {
            var file = await _contracts.GetAgreementAsync(id, ct);
            return File(file.Content, "application/pdf", file.FileName);
        }
        catch (NotFoundException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    private async Task PopulateListsAsync(ContractFormViewModel vm, bool isCreate, CancellationToken ct, ContractStatus? currentStatus = null)
    {
        var clients = await _clients.GetAllAsync(ct);
        vm.Clients = clients.Select(c => new SelectListItem($"{c.Name} ({c.Region})", c.Id.ToString()));

        // Only offer statuses the workflow allows from where the contract is now.
        var statuses = isCreate
            ? new[] { ContractStatus.Draft, ContractStatus.Active }
            : _workflow.AllowedNextStatuses(currentStatus ?? vm.Status);

        vm.StatusOptions = statuses.Select(s => new SelectListItem(
            s == ContractStatus.OnHold ? "On Hold" : s.ToString(),
            ((int)s).ToString(),
            s == vm.Status));
    }
}
