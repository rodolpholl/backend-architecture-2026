using Microsoft.AspNetCore.Http;

namespace FinControl.Infrastructure.Http;

/// <summary>
/// Utility for working with HTTP headers in a generic way.
/// Extracts and validates common values like GUIDs, correlation IDs, etc.
/// </summary>
public static class HttpHeadersHelper
{
    /// <summary>
    /// Extracts a Guid from an HTTP header or generates a new one if it doesn't exist/is invalid.
    /// </summary>
    /// <param name="headers">HTTP headers dictionary.</param>
    /// <param name="headerName">Header name (case-insensitive).</param>
    /// <param name="generateIfMissing">If true, generates new Guid if header doesn't exist. Default: true.</param>
    /// <returns>Extracted, generated, or empty Guid (if generateIfMissing=false).</returns>
    public static Guid ExtractOrGenerateGuid(
        IHeaderDictionary headers,
        string headerName,
        bool generateIfMissing = true)
    {
        if (headers == null || string.IsNullOrWhiteSpace(headerName))
            return generateIfMissing ? Guid.NewGuid() : Guid.Empty;

        if (!headers.TryGetValue(headerName, out var headerValue))
            return generateIfMissing ? Guid.NewGuid() : Guid.Empty;

        if (Guid.TryParse(headerValue.ToString(), out var guid))
            return guid;

        // Header exists but is invalid
        System.Diagnostics.Debug.WriteLine(
            $"Header '{headerName}' has invalid value: '{headerValue}'. " +
            $"{(generateIfMissing ? "Generating new UUID." : "Returning Guid.Empty.")}");

        return generateIfMissing ? Guid.NewGuid() : Guid.Empty;
    }

    /// <summary>
    /// Extracts a correlation header (x-correlation-id or custom).
    /// </summary>
    /// <param name="headers">HTTP headers dictionary.</param>
    /// <param name="headerName">Header name. Default: "x-correlation-id".</param>
    /// <returns>Correlation Guid, or new if doesn't exist/is invalid.</returns>
    public static Guid ExtractCorrelationId(
        IHeaderDictionary headers,
        string headerName = "x-correlation-id")
        => ExtractOrGenerateGuid(headers, headerName, generateIfMissing: true);

    /// <summary>
    /// Extracts an idempotency key (idempotency-key or custom).
    /// </summary>
    /// <param name="headers">HTTP headers dictionary.</param>
    /// <param name="headerName">Header name. Default: "idempotency-key".</param>
    /// <returns>Idempotency Guid, or new if doesn't exist/is invalid.</returns>
    public static Guid ExtractIdempotencyKey(
        IHeaderDictionary headers,
        string headerName = "idempotency-key")
        => ExtractOrGenerateGuid(headers, headerName, generateIfMissing: true);

    /// <summary>
    /// Extracts a generic header as string.
    /// </summary>
    /// <param name="headers">HTTP headers dictionary.</param>
    /// <param name="headerName">Header name.</param>
    /// <returns>Header value or null.</returns>
    public static string? ExtractHeader(IHeaderDictionary headers, string headerName)
    {
        if (headers == null || string.IsNullOrWhiteSpace(headerName))
            return null;

        return headers.TryGetValue(headerName, out var value)
            ? value.ToString()
            : null;
    }

    /// <summary>
    /// Extracts a generic header or returns a default value.
    /// </summary>
    /// <param name="headers">HTTP headers dictionary.</param>
    /// <param name="headerName">Header name.</param>
    /// <param name="defaultValue">Default value.</param>
    /// <returns>Header value or default value.</returns>
    public static string ExtractHeaderOrDefault(
        IHeaderDictionary headers,
        string headerName,
        string defaultValue)
        => ExtractHeader(headers, headerName) ?? defaultValue;
}
