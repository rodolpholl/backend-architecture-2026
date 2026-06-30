using FinControl.SharedKernel.Messaging;

namespace FinControl.Consolidation.Core.Features.Commands.UpdateConsolidatedBalance;

// Maintains the old name as an alias to avoid breaking the Worker until the next namespace refactor
[Obsolete("Use UpdateConsolidatedBalanceCommand")]
public record UpdateConsolidatedBalanceCommandObsolete(long TransactionAmount, DateTimeOffset TransactionDate)
    : UpdateConsolidatedBalanceCommand(TransactionAmount, TransactionDate);

public record UpdateConsolidatedBalanceCommand(long TransactionAmount, DateTimeOffset TransactionDate) : ICommand;


