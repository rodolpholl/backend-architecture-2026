using FinControl.Infrastructure.Http;
using FinControl.Transactions.Core.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Wolverine;
using Wolverine.Http;

namespace FinControl.Transactions.Core.Features.Commands.RegisterTransaction;

/// <summary>
/// Request DTO for the POST /lancamentos/registrar endpoint.
/// Input structure from the client (without audit information that comes from context).
/// </summary>
public record RegisterTransactionRequest(
    /// <summary>
    /// Transaction category.
    /// </summary>
    TransactionCategory Category,

    /// <summary>
    /// Amount in cents.
    /// </summary>
    long Amount,

    /// <summary>
    /// Optional description.
    /// </summary>
    string? Description = null,

    /// <summary>
    /// Transaction date (optional, uses UTC now if omitted).
    /// Accepts formats: "2024-05-22T10:30:00Z", "2024-05-22T10:30:00+00:00", "2024-05-22T10:30:00-03:00".
    /// </summary>
    DateTimeOffset? TransactionDate = null
);

/// <summary>
/// HTTP endpoint to register a new transaction.
/// Implements Vertical Slicing with Wolverine minimal APIs.
/// Extracts user data directly from Keycloak JWT using Infrastructure utilities.
///
/// Wolverine pattern: The "Handle" method with [WolverinePost] attribute is auto-discovered.
/// </summary>
public class RegisterTransactionEndpoint
{
    /// <summary>
    /// Endpoint handler - auto-discovered by Wolverine.
    /// </summary>
    [Authorize]
    [WolverinePost("/lancamentos/registrar")]
    public static async Task<RegisterTransactionResponse> Handle(
        RegisterTransactionRequest request,
        HttpContext httpContext,
        IMessageBus bus,
        CancellationToken cancellationToken = default)
    {
        // 1. Validate authentication
        if (httpContext.User?.Identity?.IsAuthenticated != true)
        {
            throw new InvalidOperationException("User not authenticated. JWT token is invalid or expired.");
        }

        // 2. Extract user data and tracing using Infrastructure extensions
        var (usuarioId, usuarioNome, usuarioEmail) = httpContext.ExtractUserData();
        var correlationId = httpContext.ExtractCorrelationId();
        var idempotencyKey = httpContext.ExtractIdempotencyKey();

        // 3. Build command with context data
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
