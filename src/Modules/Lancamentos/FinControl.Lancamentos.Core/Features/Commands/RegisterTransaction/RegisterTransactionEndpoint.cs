using FinControl.Infrastructure.Http;
using FinControl.Transactions.Core.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Wolverine;
using Wolverine.Http;

namespace FinControl.Transactions.Core.Features.Commands.RegisterTransaction;

/// <summary>
/// Request DTO para o endpoint POST /lancamentos/registrar.
/// Estrutura de entrada do cliente (sem informações de auditoria que vêm do contexto).
/// </summary>
public record RegisterTransactionRequest(
    /// <summary>
    /// Category do lançamento.
    /// </summary>
    TransactionCategory Category,

    /// <summary>
    /// Amount em centavos.
    /// </summary>
    long Amount,

    /// <summary>
    /// Descrição opcional.
    /// </summary>
    string? Description = null,

    /// <summary>
    /// Data do lançamento (opcional, usa UTC now se omitida).
    /// Aceita formatos: "2024-05-22T10:30:00Z", "2024-05-22T10:30:00+00:00", "2024-05-22T10:30:00-03:00".
    /// </summary>
    DateTimeOffset? TransactionDate = null
);

/// <summary>
/// Endpoint HTTP para registrar um novo lançamento.
/// Implements Vertical Slicing com Wolverine minimal APIs.
/// Extrai dados do usuário diretamente do JWT do Keycloak usando utilitários da Infrastructure.
/// 
/// Padrão Wolverine: O método "Handle" com atributo [WolverinePost] é descoberto automaticamente.
/// </summary>
public class RegisterTransactionEndpoint
{
    /// <summary>
    /// Handler do endpoint - descoberto automaticamente pelo Wolverine.
    /// </summary>
    [Authorize]
    [WolverinePost("/lancamentos/registrar")]
    public static async Task<RegisterTransactionResponse> Handle(
        RegisterTransactionRequest request,
        HttpContext httpContext,
        IMessageBus bus,
        CancellationToken cancellationToken = default)
    {
        // 1. Validar autenticação
        if (httpContext.User?.Identity?.IsAuthenticated != true)
        {
            throw new InvalidOperationException("Usuário não autenticado. Token JWT inválido ou expirado.");
        }

        // 2. Extrair dados do usuário e rastreamento usando extensões da Infrastructure
        var (usuarioId, usuarioNome, usuarioEmail) = httpContext.ExtractUserData();
        var correlationId = httpContext.ExtractCorrelationId();
        var idempotencyKey = httpContext.ExtractIdempotencyKey();

        // 3. Construir command com dados do contexto
        var command = new RegisterTransactionCommand
        {
            Category = request.Category,
            Amount = request.Amount,
            Description = request.Description,
            TransactionDate = request.TransactionDate?.ToUniversalTime() ?? DateTimeOffset.UtcNow,
            UserId = usuarioId,
            UserName = usuarioNome,
            UserEmail = usuarioEmail,
            CorrelationId = correlationId,
            IdempotencyKey = idempotencyKey,
        };

        // 4. Enviar command via Wolverine (mediator)
        var response = await bus.InvokeAsync<RegisterTransactionResponse>(command, cancellationToken);

        // 5. Adicionar headers de rastreamento na resposta
        httpContext.AddTracingHeaders(correlationId, idempotencyKey);

        return response;
    }
}
