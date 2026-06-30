using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace FinControl.Infrastructure.Middleware;

/// <summary>
/// Global handler for unhandled exceptions.
///
/// Intercepts exceptions and maps them to ProblemDetails (RFC 7807), maintaining the CorrelationId.
///
/// Mappings:
///   ValidationException (FluentValidation) → 400 with list of errors per field
///   ArgumentException                      → 400 invalid request
///   UnauthorizedAccessException            → 401
///   KeyNotFoundException                   → 404
///   InvalidOperationException              → 422
///   Exception (any other)                  → 500
///
/// Register via:
///   services.AddExceptionHandler&lt;GlobalExceptionHandler&gt;();
///   services.AddProblemDetails();
///   app.UseExceptionHandler();
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var correlationId = httpContext.Items["X-Correlation-Id"]?.ToString()
                            ?? Guid.NewGuid().ToString("D");

        // ValidationException from FluentValidation — Wolverine pipeline threw it before handler
        if (exception is ValidationException validationEx)
        {
            logger.LogWarning(
                "Validation failed. CorrelationId: {CorrelationId} | Errors: {Errors}",
                correlationId,
                string.Join(" | ", validationEx.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}")));

            var errorsByField = validationEx.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray());

            var validationProblem = new ValidationProblemDetails(errorsByField)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation error",
                Detail = "One or more fields have invalid values.",
                Extensions = { ["correlationId"] = correlationId }
            };

            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsJsonAsync(validationProblem, cancellationToken);
            return true;
        }

        // Other exceptions
        logger.LogError(
            exception,
            "Unhandled exception. CorrelationId: {CorrelationId} | Path: {Path}",
            correlationId,
            httpContext.Request.Path);

        var (status, title) = exception switch
        {
            ArgumentException           => (StatusCodes.Status400BadRequest,           "Invalid request"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized,         "Unauthorized"),
            KeyNotFoundException        => (StatusCodes.Status404NotFound,             "Resource not found"),
            InvalidOperationException   => (StatusCodes.Status422UnprocessableEntity,  "Invalid operation"),
            _                           => (StatusCodes.Status500InternalServerError,  "Internal server error")
        };

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = exception.Message,
            Extensions = { ["correlationId"] = correlationId }
        };

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
