using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FinControl.Infrastructure.Middleware;

/// <summary>
/// Valida o header X-Subscription-Key contra a chave armazenada no Vault.
///
/// Funciona como segunda camada de defesa: Kong valida na borda da rede;
/// este middleware valida na API, cobrindo requisições que cheguem por fora do gateway.
///
/// Comportamento:
///   - Rotas /health são sempre liberadas (liveness/readiness não exigem key).
///   - Se a chave não estiver configurada (Vault indisponível em dev), a validação é ignorada.
///   - Comparação é case-sensitive e em tempo constante para evitar timing attacks.
///   - Retorna 401 ProblemDetails quando a key está ausente ou incorreta.
/// </summary>
public sealed class SubscriptionKeyMiddleware(RequestDelegate next, string configurationKey)
{
    private const string HeaderName = "X-Subscription-Key";

    public async Task InvokeAsync(
        HttpContext context,
        IConfiguration configuration,
        ILogger<SubscriptionKeyMiddleware> logger)
    {
        if (context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase) ||
            context.Request.Path.StartsWithSegments("/metrics", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var expectedKey = configuration[configurationKey];

        if (string.IsNullOrEmpty(expectedKey))
        {
            // Vault indisponível (ex: dev sem docker) — validação desativada
            await next(context);
            return;
        }

        context.Request.Headers.TryGetValue(HeaderName, out var providedKey);

        if (!ValidKey(providedKey, expectedKey))
        {
            var correlationId = context.Items["X-Correlation-Id"]?.ToString();

            logger.LogWarning(
                "Subscription key inválida ou ausente. CorrelationId={CorrelationId} Path={Path} IP={IP}",
                correlationId,
                context.Request.Path,
                context.Connection.RemoteIpAddress);

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/problem+json";

            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Subscription key inválida",
                Detail = "O header X-Subscription-Key está ausente ou é inválido.",
                Extensions = { ["correlationId"] = correlationId }
            });
            return;
        }

        await next(context);
    }

    // CryptographicOperations.FixedTimeEquals evita timing attack por comparação de strings
    private static bool ValidKey(string? provided, string expected)
    {
        if (string.IsNullOrEmpty(provided))
            return false;

        var providedBytes = System.Text.Encoding.UTF8.GetBytes(provided);
        var expectedBytes = System.Text.Encoding.UTF8.GetBytes(expected);

        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            providedBytes, expectedBytes);
    }
}

public static class SubscriptionKeyMiddlewareExtensions
{
    /// <summary>
    /// Adiciona validação de subscription key ao pipeline HTTP.
    /// </summary>
    /// <param name="configurationKey">
    /// Chave do IConfiguration que contém a subscription key esperada.
    /// Use as constantes de <see cref="FinControl.Infrastructure.Vault.VaultKeys"/>.
    /// </param>
    public static IApplicationBuilder UseSubscriptionKeyValidation(
        this IApplicationBuilder app,
        string configurationKey) =>
        app.UseMiddleware<SubscriptionKeyMiddleware>(configurationKey);
}
