using Microsoft.AspNetCore.Http;

namespace FinControl.Infrastructure.Http;

/// <summary>
/// Extensões para HttpContext para facilitar extração de dados comuns.
/// </summary>
public static class HttpContextExtensions
{
    /// <summary>
    /// Extrai dados de identificação do usuário autenticado via JWT.
    /// </summary>
    /// <param name="httpContext">Contexto HTTP.</param>
    /// <returns>Tupla com (ID, Nome, Email) do usuário.</returns>
    /// <exception cref="InvalidOperationException">Se usuário não autenticado ou claims ausentes.</exception>
    public static (string Id, string Nome, string Email) ExtractUserData(this HttpContext httpContext)
    {
        if (httpContext?.User == null)
            throw new InvalidOperationException("HttpContext.User é nulo.");

        return JwtClaimsExtractor.ExtractUserData(httpContext.User);
    }

    /// <summary>
    /// Extrai a correlation ID do header ou gera uma nova.
    /// </summary>
    /// <param name="httpContext">Contexto HTTP.</param>
    /// <param name="headerName">Nome do header. Padrão: "x-correlation-id".</param>
    /// <returns>Guid da correlação.</returns>
    public static Guid ExtractCorrelationId(
        this HttpContext httpContext,
        string headerName = "x-correlation-id")
    {
        if (httpContext?.Request?.Headers == null)
            return Guid.NewGuid();

        return HttpHeadersHelper.ExtractCorrelationId(httpContext.Request.Headers, headerName);
    }

    /// <summary>
    /// Extrai a chave de idempotência do header ou gera uma nova.
    /// </summary>
    /// <param name="httpContext">Contexto HTTP.</param>
    /// <param name="headerName">Nome do header. Padrão: "idempotency-key".</param>
    /// <returns>Guid de idempotência.</returns>
    public static Guid ExtractIdempotencyKey(
        this HttpContext httpContext,
        string headerName = "idempotency-key")
    {
        if (httpContext?.Request?.Headers == null)
            return Guid.NewGuid();

        return HttpHeadersHelper.ExtractIdempotencyKey(httpContext.Request.Headers, headerName);
    }

    /// <summary>
    /// Extrai um header genérico do request.
    /// </summary>
    /// <param name="httpContext">Contexto HTTP.</param>
    /// <param name="headerName">Nome do header.</param>
    /// <returns>Amount do header ou null.</returns>
    public static string? ExtractHeader(this HttpContext httpContext, string headerName)
    {
        if (httpContext?.Request?.Headers == null)
            return null;

        return HttpHeadersHelper.ExtractHeader(httpContext.Request.Headers, headerName);
    }

    /// <summary>
    /// Adiciona um header à resposta HTTP.
    /// </summary>
    /// <param name="httpContext">Contexto HTTP.</param>
    /// <param name="headerName">Nome do header.</param>
    /// <param name="headerValue">Amount do header.</param>
    public static void AddResponseHeader(
        this HttpContext httpContext,
        string headerName,
        string headerValue)
    {
        if (httpContext?.Response?.Headers != null)
            httpContext.Response.Headers[headerName] = headerValue;
    }

    /// <summary>
    /// Adiciona headers de rastreamento (correlation ID e idempotency key) à resposta.
    /// </summary>
    /// <param name="httpContext">Contexto HTTP.</param>
    /// <param name="correlationId">ID de correlação.</param>
    /// <param name="idempotencyKey">Chave de idempotência.</param>
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
