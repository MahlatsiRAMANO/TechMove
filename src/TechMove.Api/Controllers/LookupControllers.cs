using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechMove.Api.Dtos;
using TechMove.Api.Services;
using TechMove.Api.Services.Currency;

namespace TechMove.Api.Controllers;

[ApiController]
[Route("api/exchange-rates")]
[Authorize]
[Produces("application/json")]
public sealed class ExchangeRatesController : ControllerBase
{
    private readonly IExchangeRateProvider _rates;

    public ExchangeRatesController(IExchangeRateProvider rates) => _rates = rates;

    /// <summary>The current USD → ZAR exchange rate from the external currency API.</summary>
    [HttpGet("usd-zar")]
    [ProducesResponseType(typeof(ExchangeRateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ExchangeRateDto>> GetUsdToZar(CancellationToken ct)
    {
        var result = await _rates.GetUsdToZarRateAsync(ct);
        if (!result.Success)
        {
            throw new ExternalServiceException(result.Error ?? "The exchange rate is unavailable right now.");
        }

        return Ok(new ExchangeRateDto("USD", "ZAR", result.Rate, result.RetrievedAtUtc ?? DateTime.UtcNow));
    }
}

[ApiController]
[Route("api/dashboard")]
[Authorize]
[Produces("application/json")]
public sealed class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboard;

    public DashboardController(IDashboardService dashboard) => _dashboard = dashboard;

    /// <summary>Counts and totals for the home page.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(DashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardDto>> Get(CancellationToken ct) => Ok(await _dashboard.GetSummaryAsync(ct));
}
