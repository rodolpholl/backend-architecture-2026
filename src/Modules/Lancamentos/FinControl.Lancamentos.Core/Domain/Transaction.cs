using FinControl.Transactions.Core.Domain.Enums;
using FinControl.SharedKernel.Domain;

namespace FinControl.Transactions.Core.Domain;

public class Transaction : DomainEntity<long>, IAuditableDomainEntity, ISoftDeleteDomainEntity
{
    public TransactionCategory Category { get; set; }
    public long Amount { get; set; }
    public decimal FormattedAmount => Amount / 100m;

    public TransactionType Type => Amount < 0 ? TransactionType.Debit : TransactionType.Credit;

    // Switch expression eliminates reflection on each access
    public string FormattedType => Type switch
    {
        TransactionType.Credit => "Credit",
        TransactionType.Debit  => "Debit",
        _                      => Type.ToString()
    };

    public string? Description { get; set; } = string.Empty;
    public DateTimeOffset TransactionDate { get; set; }

    /// <summary>
    /// Chave de idempotência enviada pelo cliente via X-Idempotency-Key.
    /// Garante que retransmissões RabbitMQ não criem lançamentos duplicados.
    /// </summary>
    public Guid IdempotencyKey { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string CreatedByName { get; set; } = string.Empty;
    public string CreatedByEmail { get; set; } = string.Empty;
    public string? UpdatedBy { get; set; }
    public string? UpdatedByName { get; set; }
    public string? UpdatedByEmail { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}
