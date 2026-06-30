using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace FinControl.Infrastructure.Middleware;

/// <summary>
/// Handler global de exceções não tratadas.
///
/// Intercepta exceções e mapeia para ProblemDetails (RFC 7807), mantendo o CorrelationId.
///
/// Mapeamentos:
///   ValidationException (FluentValidation) → 400 com lista de erros por campo
///   ArgumentException                      → 400 requisição inválida
///   UnauthorizedAccessException            → 401
///   KeyNotFoundException                   → 404
///   InvalidOperationException              → 422
///   Exception (qualquer outra)             → 500
///
/// Registrar via:
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

        // ValidationException do FluentValidation — pipeline do Wolverine a lançou antes do handler
        if (exception is ValidationException validationEx)
        {
            logger.LogWarning(
                "Validação falhou. CorrelationId: {CorrelationId} | Erros: {Errors}",
                correlationId,
                string.Join(" | ", validationEx.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}")));

            var errosPorCampo = validationEx.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray());

            var validationProblem = new ValidationProblemDetails(errosPorCampo)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Erro de validação",
                Detail = "Um ou mais campos possuem valores inválidos.",
                Extensions = { ["correlationId"] = correlationId }
            };

            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsJsonAsync(validationProblem, cancellationToken);
            return true;
        }

        // Demais exceções
        logger.LogError(
            exception,
            "Exceção não tratada. CorrelationId: {CorrelationId} | Path: {Path}",
            correlationId,
            httpContext.Request.Path);

        var (status, title) = exception switch
        {
            ArgumentException           => (StatusCodes.Status400BadRequest,           "Requisição inválida"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized,         "Não autorizado"),
            KeyNotFoundException        => (StatusCodes.Status404NotFound,             "Recurso não encontrado"),
            InvalidOperationException   => (StatusCodes.Status422UnprocessableEntity,  "Operação inválida"),
            _                           => (StatusCodes.Status500InternalServerError,  "Erro interno do servidor")
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
