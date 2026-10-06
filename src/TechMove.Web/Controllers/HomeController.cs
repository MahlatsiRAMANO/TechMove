using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TechMove.Web.Data.Repositories;
using TechMove.Web.Models;
using TechMove.Web.ViewModels;

namespace TechMove.Web.Controllers;

public class HomeController : Controller
{
    private readonly IClientRepository _clients;
    private readonly IContractRepository _contracts;
    private readonly IServiceRequestRepository _requests;

    public HomeController(IClientRepository clients, IContractRepository contracts, IServiceRequestRepository requests)
    {
        _clients = clients;
        _contracts = contracts;
        _requests = requests;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        // Sequential awaits: a single DbContext must not run queries in parallel.
        var vm = new DashboardViewModel
        {
            ClientCount = (await _clients.GetAllAsync(ct)).Count,
            ActiveContracts = await _contracts.CountByStatusAsync(ContractStatus.Active, ct),
            DraftContracts = await _contracts.CountByStatusAsync(ContractStatus.Draft, ct),
            OnHoldContracts = await _contracts.CountByStatusAsync(ContractStatus.OnHold, ct),
            ExpiredContracts = await _contracts.CountByStatusAsync(ContractStatus.Expired, ct),
            ServiceRequestCount = await _requests.CountAsync(ct),
            TotalServiceCostZar = await _requests.TotalCostAsync(ct)
        };
        return View(vm);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
