using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechMove.Web.Models;
using TechMove.Web.Services.Api;

namespace TechMove.Web.Controllers;

public class HomeController : Controller
{
    private readonly IDashboardApi _dashboard;

    public HomeController(IDashboardApi dashboard) => _dashboard = dashboard;

    public async Task<IActionResult> Index(CancellationToken ct) => View(await _dashboard.GetAsync(ct));

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
