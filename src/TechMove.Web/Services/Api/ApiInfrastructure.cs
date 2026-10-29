using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TechMove.Web.Services.Api;

public sealed class ApiOptions
{
    public const string SectionName = "Api";

    /// <summary>Base URL of the TechMove API, e.g. https://localhost:7181/ or http://glms-backend-api:8080/ in Docker.</summary>
    public string BaseUrl { get; set; } = "https://localhost:7181/";

    public int TimeoutSeconds { get; set; } = 30;
}

// ---- Exceptions the frontend understands (controllers turn these into friendly messages) ----

/// <summary>Base type for any failed API call.</summary>
public abstract class ApiException : Exception
{
    protected ApiException(string message, HttpStatusCode? statusCode = null, Exception? inner = null) : base(message, inner)
        => StatusCode = statusCode;

    public HttpStatusCode? StatusCode { get; }
}

/// <summary>400 - the API rejected the input or a business rule. Carries field errors when available.</summary>
public sealed class ApiValidationException : ApiException
{
    public ApiValidationException(string message, IReadOnlyDictionary<string, string[]> errors)
        : base(message, HttpStatusCode.BadRequest) => Errors = errors;

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}

/// <summary>404 - the record does not exist.</summary>
public sealed class ApiNotFoundException : ApiException
{
    public ApiNotFoundException(string message) : base(message, HttpStatusCode.NotFound) { }
}

/// <summary>401 / 403 - token missing, expired or not allowed.</summary>
public sealed class ApiUnauthorizedException : ApiException
{
    public ApiUnauthorizedException(string message, HttpStatusCode statusCode) : base(message, statusCode) { }

    public bool IsForbidden => StatusCode == HttpStatusCode.Forbidden;
}

/// <summary>The API is down, timed out, or returned 5xx (e.g. 503 when the currency API is unavailable).</summary>
public sealed class ApiUnavailableException : ApiException
{
    public ApiUnavailableException(string message, HttpStatusCode? statusCode = null, Exception? inner = null)
        : base(message, statusCode, inner) { }
}

/// <summary>
/// Base class for the typed API clients. Sends requests, deserialises JSON, and converts every
/// failure (HTTP error codes, timeouts, connection refused) into one of the ApiException types.
/// </summary>
public abstract class ApiClientBase
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    protected ApiClientBase(HttpClient http) => Http = http;

    protected HttpClient Http { get; }

    protected Task<T> GetAsync<T>(string url, CancellationToken ct) where T : class =>
        SendAsync<T>(() => new HttpRequestMessage(HttpMethod.Get, url), ct);

    protected Task<T> PostJsonAsync<T>(string url, object body, CancellationToken ct) where T : class =>
        SendAsync<T>(() => new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body, options: Json) }, ct);

    protected Task<T> PatchJsonAsync<T>(string url, object body, CancellationToken ct) where T : class =>
        SendAsync<T>(() => new HttpRequestMessage(HttpMethod.Patch, url) { Content = JsonContent.Create(body, options: Json) }, ct);

    protected Task PutJsonAsync(string url, object body, CancellationToken ct) =>
        SendAsync(() => new HttpRequestMessage(HttpMethod.Put, url) { Content = JsonContent.Create(body, options: Json) }, ct);

    protected Task DeleteAsync(string url, CancellationToken ct) =>
        SendAsync(() => new HttpRequestMessage(HttpMethod.Delete, url), ct);

    protected async Task<T> SendAsync<T>(Func<HttpRequestMessage> build, CancellationToken ct) where T : class
    {
        using var response = await SendRawAsync(build(), ct);
        try
        {
            var result = await response.Content.ReadFromJsonAsync<T>(Json, ct);
            return result ?? throw new ApiUnavailableException("The TechMove API returned an empty response.");
        }
        catch (JsonException ex)
        {
            throw new ApiUnavailableException("The TechMove API returned data in an unexpected format.", response.StatusCode, ex);
        }
    }

    protected async Task SendAsync(Func<HttpRequestMessage> build, CancellationToken ct)
    {
        using var response = await SendRawAsync(build(), ct);
    }

    /// <summary>Sends the request and throws the matching ApiException for any non-success response.
    /// The caller owns (and must dispose) the returned response.</summary>
    protected async Task<HttpResponseMessage> SendRawAsync(HttpRequestMessage request, CancellationToken ct, HttpCompletionOption completion = HttpCompletionOption.ResponseContentRead)
    {
        HttpResponseMessage response;
        try
        {
            response = await Http.SendAsync(request, completion, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiUnavailableException("The TechMove API cannot be reached. Please make sure it is running and try again.", null, ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new ApiUnavailableException("The TechMove API took too long to respond. Please try again.", null, ex);
        }
        finally
        {
            request.Dispose();
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        using (response)
        {
            var problem = await ReadProblemAsync(response, ct);
            var message = problem?.Detail ?? problem?.Title ?? $"The TechMove API returned {(int)response.StatusCode} {response.ReasonPhrase}.";

            ApiException error = response.StatusCode switch
            {
                HttpStatusCode.BadRequest or HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity =>
                    new ApiValidationException(
                        problem?.Errors is { Count: > 0 } ? "Please correct the highlighted fields." : message,
                        problem?.Errors ?? new Dictionary<string, string[]>()),
                HttpStatusCode.NotFound => new ApiNotFoundException(problem?.Detail ?? "The requested record was not found."),
                HttpStatusCode.Unauthorized => new ApiUnauthorizedException("Your session has expired. Please sign in again.", response.StatusCode),
                HttpStatusCode.Forbidden => new ApiUnauthorizedException("You do not have permission to do that.", response.StatusCode),
                _ => new ApiUnavailableException(message, response.StatusCode)
            };
            throw error;
        }
    }

    private static async Task<ProblemResponse?> ReadProblemAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is null || !mediaType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<ProblemResponse>(Json, ct);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>RFC 7807 problem details as returned by the API.</summary>
    private sealed class ProblemResponse
    {
        public string? Title { get; set; }
        public string? Detail { get; set; }
        public int? Status { get; set; }
        public Dictionary<string, string[]>? Errors { get; set; }
    }
}

/// <summary>
/// Adds "Authorization: Bearer &lt;jwt&gt;" to every API call, using the token stored in the
/// signed-in user's authentication cookie.
/// </summary>
public sealed class BearerTokenHandler : DelegatingHandler
{
    public const string TokenClaim = "access_token";

    private readonly IHttpContextAccessor _accessor;

    public BearerTokenHandler(IHttpContextAccessor accessor) => _accessor = accessor;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = _accessor.HttpContext?.User.FindFirst(TokenClaim)?.Value;
        if (!string.IsNullOrEmpty(token) && request.Headers.Authorization is null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
