using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TechMove.Api.Services;

namespace TechMove.Api.Infrastructure;

/// <summary>
/// Turns the service layer's exceptions into proper HTTP responses (RFC 7807 problem details),
/// so controllers stay thin and every endpoint reports errors the same way.
/// </summary>
public sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails;
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger)
    {
        _problemDetails = problemDetails;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            InvalidFileException => (StatusCodes.Status400BadRequest, "Invalid file"),
            BusinessRuleException => (StatusCodes.Status400BadRequest, "Business rule violated"),
            ExternalServiceException => (StatusCodes.Status503ServiceUnavailable, "Dependency unavailable"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error")
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            // Expected (user-facing) errors carry their message; unexpected ones don't leak internals.
            Detail = status == StatusCodes.Status500InternalServerError
                ? "An unexpected error occurred. Please try again."
                : exception.Message
        };

        httpContext.Response.StatusCode = status;
        var written = await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem
        });

        if (!written)
        {
            await httpContext.Response.WriteAsJsonAsync(problem, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json", cancellationToken);
        }

        return true;
    }
}
