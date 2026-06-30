using Microsoft.AspNetCore.Http;

namespace FinControl.Infrastructure.Http;

/// <summary>
/// Utilitário para trabalhar com headers HTTP de forma genérica.
/// Extrai e valida valores comuns como GUIDs, correlation IDs, etc.
/// </summary>
public static class HttpHeadersHelper
{
    /// <summary>
    /// Extrai um Guid de um header HTTP ou gera um novo se não existir/for inválido.
    /// </summary>
    /// <param name="headers">Dicionário de headers HTTP.</param>
    /// <param name="headerName">Nome do header (case-insensitive).</param>
    /// <param name="generateIfMissing">Se true, gera novo Guid se header não existir. Padrão: true.</param>
    /// <returns>Guid extraído, gerado ou vazio (se generateIfMissing=false).</returns>
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

        // Header existe mas é inválido
        System.Diagnostics.Debug.WriteLine(
            $"Header '{headerName}' tem valor inválido: '{headerValue}'. " +
            $"{(generateIfMissing ? "Gerando novo UUID." : "Retornando Guid.Empty.")}");

        return generateIfMissing ? Guid.NewGuid() : Guid.Empty;
    }

    /// <summary>
    /// Extrai um header de correlação (x-correlation-id ou customizado).
    /// </summary>
    /// <param name="headers">Dicionário de headers HTTP.</param>
    /// <param name="headerName">Nome do header. Padrão: "x-correlation-id".</param>
    /// <returns>Guid da correlação, ou novo se não existir/for inválido.</returns>
    public static Guid ExtractCorrelationId(
        IHeaderDictionary headers,
        string headerName = "x-correlation-id")
        => ExtractOrGenerateGuid(headers, headerName, generateIfMissing: true);

    /// <summary>
    /// Extrai uma chave de idempotência (idempotency-key ou customizada).
    /// </summary>
    /// <param name="headers">Dicionário de headers HTTP.</param>
    /// <param name="headerName">Nome do header. Padrão: "idempotency-key".</param>
    /// <returns>Guid de idempotência, ou novo se não existir/for inválido.</returns>
    public static Guid ExtractIdempotencyKey(
        IHeaderDictionary headers,
        string headerName = "idempotency-key")
        => ExtractOrGenerateGuid(headers, headerName, generateIfMissing: true);

    /// <summary>
    /// Extrai um header genérico como string.
    /// </summary>
    /// <param name="headers">Dicionário de headers HTTP.</param>
    /// <param name="headerName">Nome do header.</param>
    /// <returns>Amount do header ou null.</returns>
    public static string? ExtractHeader(IHeaderDictionary headers, string headerName)
    {
        if (headers == null || string.IsNullOrWhiteSpace(headerName))
            return null;

        return headers.TryGetValue(headerName, out var value)
            ? value.ToString()
            : null;
    }

    /// <summary>
    /// Extrai um header genérico ou retorna um valor padrão.
    /// </summary>
    /// <param name="headers">Dicionário de headers HTTP.</param>
    /// <param name="headerName">Nome do header.</param>
    /// <param name="defaultValue">Amount padrão.</param>
    /// <returns>Amount do header ou valor padrão.</returns>
    public static string ExtractHeaderOuPadrão(
        IHeaderDictionary headers,
        string headerName,
        string defaultValue)
        => ExtractHeader(headers, headerName) ?? defaultValue;
}
