using FinControl.Consolidation.Core.Domain;
using FinControl.Infrastructure.Cache;
using Microsoft.Extensions.Logging;

namespace FinControl.Consolidation.Core.Features.Commands.UpdateConsolidatedBalance;

public class UpdateConsolidatedBalanceCommandHandler(
    RedisCacheService cache,
    IRedisLockService lockService,
    ILogger<UpdateConsolidatedBalanceCommandHandler> logger)
{
    private const string CACHE_KEY_ACCUMULATED = "balance:consolidated:accumulated";

    private static string CacheKey(DateOnly data) => $"balance:consolidated:{data:yyyy-MM-dd}";
    private static string LockKey(DateOnly data) => $"lock:balance:consolidated:{data:yyyy-MM-dd}";

    public async Task Handle(
        UpdateConsolidatedBalanceCommand command,
        CancellationToken cancellationToken = default)
    {
        // Uses transaction date — not current date — to consolidate on the correct day
        var date = DateOnly.FromDateTime(command.TransactionDate.UtcDateTime);
        var key = CacheKey(date);

        var acquired = await lockService.ExecuteWithLockAsync(
            lockKey: LockKey(date),
            action: async () =>
            {
                // Updating accumulated balance
                var keyTotalAccumulated = await cache.GetAsync<ConsolidatedBalance>(CACHE_KEY_ACCUMULATED, cancellationToken);

                var valueConsolidatedBalance = (keyTotalAccumulated?.Balance ?? 0) + command.TransactionAmount;

                await cache.SetAsync(CACHE_KEY_ACCUMULATED, new ConsolidatedBalance(
                    Balance: valueConsolidatedBalance,
                    LastUpdated: DateTimeOffset.UtcNow), null, cancellationToken);

                // Updating daily balance for queries for a specific day
                var current = await cache.GetAsync<ConsolidatedBalance>(key, cancellationToken);

                var previousBalance = current?.Balance ?? 0;
                var newBalance = new ConsolidatedBalance(
                    Balance: valueConsolidatedBalance,
                    LastUpdated: DateTimeOffset.UtcNow);

                await cache.SetAsync(key, newBalance, TimeSpan.FromDays(30), cancellationToken);

                if (logger.IsEnabled(LogLevel.Information))
                    logger.LogInformation(
                        "Consolidated balance updated | Date={Date} PreviousBalance={PreviousBalance} Increment={Increment} NewBalance={NewBalance} CacheKey={CacheKey}",
                        date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                        previousBalance,
                        command.TransactionAmount,
                        newBalance.Balance,
                        key);
            },
            lockExpiry: TimeSpan.FromSeconds(10),
            ct: cancellationToken);

        if (!acquired)
            throw new InvalidOperationException(
                $"Could not acquire consolidation lock for date {date:yyyy-MM-dd}. " +
                "The event will be reprocessed by the message broker.");
    }
}

