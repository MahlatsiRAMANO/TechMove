using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace TechMove.Api.Services.Currency;

public sealed class ExchangeRateApiOptions
{
    public const string SectionName = "ExchangeRateApi";

    /// <summary>Free, key-less API: https://open.er-api.com/v6/latest/USD</summary>
    public string BaseUrl { get; set; } = "https://open.er-api.com/";
    public string LatestUsdPath { get; set; } = "v6/latest/USD";
    public int TimeoutSeconds { get; set; } = 10;
    public int CacheMinutes { get; set; } = 30;
}

public sealed record ExchangeRateResult(bool Success, decimal Rate, DateTime? RetrievedAtUtc, string? Error)
{
    public static ExchangeRateResult Ok(decimal rate, DateTime retrievedAtUtc) => new(true, rate, retrievedAtUtc, null);
    public static ExchangeRateResult Fail(string error) => new(false, 0m, null, error);
}

/// <summary>
/// Strategy pattern: the app depends on this abstraction, so the rate source
/// (ExchangeRate-API, OpenExchangeRates, a fake in tests) can be swapped without touching business logic.
/// </summary>
public interface IExchangeRateProvider
{
    Task<ExchangeRateResult> GetUsdToZarRateAsync(CancellationToken ct = default);
}

/// <summary>Consumes ExchangeRate-API with a typed HttpClient, fully async (no .Result / .Wait()).</summary>
public sealed class ExchangeRateApiProvider : IExchangeRateProvider
{
    private const string CacheKey = "fx:USD:ZAR";

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ExchangeRateApiProvider> _logger;
    private readonly ExchangeRateApiOptions _options;

    public ExchangeRateApiProvider(
        HttpClient http,
        IMemoryCache cache,
        ILogger<ExchangeRateApiProvider> logger,
        IOptions<ExchangeRateApiOptions> options)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<ExchangeRateResult> GetUsdToZarRateAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out ExchangeRateResult? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            using var response = await _http.GetAsync(_options.LatestUsdPath, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Exchange rate API returned {StatusCode}", (int)response.StatusCode);
                return ExchangeRateResult.Fail($"The currency service returned an error ({(int)response.StatusCode}). Please try again later.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;

            if (root.TryGetProperty("result", out var result) &&
                !string.Equals(result.GetString(), "success", StringComparison.OrdinalIgnoreCase))
            {
                return ExchangeRateResult.Fail("The currency service could not provide a rate right now.");
            }

            if (!root.TryGetProperty("rates", out var rates) ||
                !rates.TryGetProperty("ZAR", out var zar) ||
                !zar.TryGetDecimal(out var rate) ||
                rate <= 0)
            {
                return ExchangeRateResult.Fail("The currency service response did not contain a valid ZAR rate.");
            }

            var ok = ExchangeRateResult.Ok(rate, DateTime.UtcNow);
            _cache.Set(CacheKey, ok, TimeSpan.FromMinutes(_options.CacheMinutes));
            return ok;
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Exchange rate API timed out");
            return ExchangeRateResult.Fail("The currency service timed out. Please try again.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Exchange rate API is unreachable");
            return ExchangeRateResult.Fail("The currency service is currently unavailable. Please try again later.");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Exchange rate API returned invalid JSON");
            return ExchangeRateResult.Fail("The currency service returned an unreadable response.");
        }
    }
}
