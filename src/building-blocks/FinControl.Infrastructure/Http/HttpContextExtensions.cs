using Microsoft.AspNetCore.Http;

namespace FinControl.Infrastructure.Http;

/// <summary>
/// Extensions for HttpContext to facilitate common data extraction.
/// </summary>
public static class HttpContextExtensions
{
    /// <summary>
    /// Extracts user identification data authenticated via JWT.
    /// </summary>
    /// <param name="httpContext">HTTP context.</param>
    /// <returns>Tuple with (ID, Name, Email) of the user.</returns>
    /// <exception cref="InvalidOperationException">If user not authenticated or claims missing.</exception>
    public static (string Id, string Nome, string Email) ExtractUserData(this HttpContext httpContext)
    {
        if (httpContext?.User == null)
            throw new InvalidOperationException("HttpContext.User is null.");

        return JwtClaimsExtractor.ExtractUserData(httpContext.User);
    }

    /// <summary>
    /// Extracts the correlation ID from the header or generates a new one.
    /// </summary>
    /// <param name="httpContext">HTTP context.</param>
    /// <param name="headerName">Header name. Default: "x-correlation-id".</param>
    /// <returns>Correlation Guid.</returns>
    public static Guid ExtractCorrelationId(
        this HttpContext httpContext,
        string headerName = "x-correlation-id")
    {
        if (httpContext?.Request?.Headers == null)
            return Guid.NewGuid();

        return HttpHeadersHelper.ExtractCorrelationId(httpContext.Request.Headers, headerName);
    }

    /// <summary>
    /// Extracts the idempotency key from the header or generates a new one.
    /// </summary>
    /// <param name="httpContext">HTTP context.</param>
    /// <param name="headerName">Header name. Default: "idempotency-key".</param>
    /// <returns>Idempotency Guid.</returns>
    public static Guid ExtractIdempotencyKey(
        this HttpContext httpContext,
        string headerName = "idempotency-key")
    {
        if (httpContext?.Request?.Headers == null)
            return Guid.NewGuid();

        return HttpHeadersHelper.ExtractIdempotencyKey(httpContext.Request.Headers, headerName);
    }

    /// <summary>
    /// Extracts a generic header from the request.
    /// </summary>
    /// <param name="httpContext">HTTP context.</param>
    /// <param name="headerName">Header name.</param>
    /// <returns>Header value or null.</returns>
    public static string? ExtractHeader(this HttpContext httpContext, string headerName)
    {
        if (httpContext?.Request?.Headers == null)
            return null;

        return HttpHeadersHelper.ExtractHeader(httpContext.Request.Headers, headerName);
    }

    /// <summary>
    /// Adds a header to the HTTP response.
    /// </summary>
    /// <param name="httpContext">HTTP context.</param>
    /// <param name="headerName">Header name.</param>
    /// <param name="headerValue">Header value.</param>
    public static void AddResponseHeader(
        this HttpContext httpContext,
        string headerName,
        string headerValue)
    {
        if (httpContext?.Response?.Headers != null)
            httpContext.Response.Headers[headerName] = headerValue;
    }

    /// <summary>
    /// Adds tracing headers (correlation ID and idempotency key) to the response.
    /// </summary>
    /// <param name="httpContext">HTTP context.</param>
    /// <param name="correlationId">Correlation ID.</param>
    /// <param name="idempotencyKey">Idempotency key.</param>
    public static void AddTracingHeaders(
        this HttpContext httpContext,
        Guid correlationId,
        Guid idempotencyKey)
    {
        if (httpContext?.Response?.Headers != null)
        {
            httpContext.Response.Headers["x-correlation-id"] = correlationId.ToString("D");
            httpContext.Response.Headers["idempotency-key"] = idempotencyKey.ToString("D");
        }
    }
}
