using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace FinControl.Infrastructure.Middleware;

/// <summary>
/// Extrai o X-Correlation-Id do header da requisição (ou gera um novo) e:
/// - Propaga para o response
/// - Empurra para o LogContext do Serilog (aparece em todos os logs da requisição)
/// - Armazena em HttpContext.Items para uso interno
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrGenerate(context);

        context.Items[HeaderName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        // Enriquece TODOS os logs desta requisição com o CorrelationId
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }

    private static string GetOrGenerate(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var existing) &&
            !string.IsNullOrWhiteSpace(existing))
            return existing.ToString();

        return Guid.NewGuid().ToString("D");
    }
}
