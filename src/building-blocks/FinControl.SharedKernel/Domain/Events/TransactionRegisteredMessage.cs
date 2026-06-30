namespace FinControl.SharedKernel.Domain.Events;

/// <summary>
/// Shared event contract between the Transactions and Consolidated modules.
/// Published by Transactions after persistence and consumed by the Consolidated Worker.
/// </summary>
public record TransactionRegisteredMessage(
    long Id,
    Guid NavigationId,
    EventTransactionCategory Category,
    long Amount,
    string? Description,
    DateTimeOffset TransactionDate,
    DateTimeOffset OccurredAt,
    string UserId,
    string UserName,
    string UserEmail,
    Guid CorrelationId
);

/// <summary>
/// Transaction category enum for the event contract — values must remain in parity
/// with FinControl.Transactions.Core.Domain.Enums.TransactionCategory.
/// </summary>
public enum EventTransactionCategory
{
    Sale = 1,
    Return = 2,
    CashSupply = 3,
    CashWithdrawal = 4,
    SupplierPayment = 5,
    DebtCollection = 6,
    Others = 7
}
