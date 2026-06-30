using FinControl.Entries.Core.Domain.Enums;
using FinControl.SharedKernel.Messaging;

namespace FinControl.Entries.Core.Features.Commands.RegisterTransaction;

/// <summary>
/// Command to register a new transaction in the system.
/// Encapsulates the data required to create a transaction with business validation.
/// </summary>
public record RegisterTransactionCommand : ICommand<RegisterTransactionResponse>
{
    /// <summary>
    /// Transaction category (Sale, Return, CashSupply, etc).
    /// </summary>
    public TransactionCategory Category { get; init; }

    /// <summary>
    /// Amount in cents (to avoid precision issues with decimals).
    /// Ex: 15.50 = 1550 cents.
    /// </summary>
    public long Amount { get; init; }

    /// <summary>
    /// Optional transaction description (required if Category = Others).
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Transaction date. If not provided, uses the current date.
    /// </summary>
    public DateTimeOffset TransactionDate { get; init; }

    /// <summary>
    /// ID of the user creating the transaction (extracted from Keycloak via context).
    /// </summary>
    public required string UserId { get; init; }

    /// <summary>
    /// User's full name (denormalized snapshot).
    /// </summary>
    public required string UserName { get; init; }

    /// <summary>
    /// User's email (denormalized snapshot).
    /// </summary>
    public required string UserEmail { get; init; }

    /// <summary>
    /// Idempotency key to prevent duplication in case of retry.
    /// </summary>
    public Guid IdempotencyKey { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Correlation ID for distributed tracing.
    /// </summary>
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}

