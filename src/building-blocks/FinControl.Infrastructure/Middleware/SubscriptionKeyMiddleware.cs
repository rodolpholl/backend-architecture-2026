using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FinControl.Infrastructure.Middleware;

/// <summary>
/// Validates the X-Subscription-Key header against the key stored in Vault.
///
/// Works as a second layer of defense: Kong validates at the network edge;
/// this middleware validates in the API, covering requests that come from outside the gateway.
///
/// Behavior:
///   - /health routes are always allowed (liveness/readiness don't require a key).
///   - If the key is not configured (Vault unavailable in dev), validation is skipped.
///   - Comparison is case-sensitive and constant-time to prevent timing attacks.
///   - Returns 401 ProblemDetails when the key is absent or incorrect.
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
            // Vault unavailable (ex: dev without docker) — validation disabled
            await next(context);
            return;
        }

        context.Request.Headers.TryGetValue(HeaderName, out var providedKey);

        if (!ValidKey(providedKey, expectedKey))
        {
            var correlationId = context.Items["X-Correlation-Id"]?.ToString();

            logger.LogWarning(
                "Subscription key invalid or missing. CorrelationId={CorrelationId} Path={Path} IP={IP}",
                correlationId,
                context.Request.Path,
                context.Connection.RemoteIpAddress);

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/problem+json";

            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Invalid subscription key",
                Detail = "The X-Subscription-Key header is missing or invalid.",
                Extensions = { ["correlationId"] = correlationId }
            });
            return;
        }

        await next(context);
    }

    // CryptographicOperations.FixedTimeEquals avoids timing attack from string comparison
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
    /// Adds subscription key validation to the HTTP pipeline.
    /// </summary>
    /// <param name="configurationKey">
    /// IConfiguration key that contains the expected subscription key.
    /// Use the constants from <see cref="FinControl.Infrastructure.Vault.VaultKeys"/>.
    /// </param>
    public static IApplicationBuilder UseSubscriptionKeyValidation(
        this IApplicationBuilder app,
        string configurationKey) =>
        app.UseMiddleware<SubscriptionKeyMiddleware>(configurationKey);
}
