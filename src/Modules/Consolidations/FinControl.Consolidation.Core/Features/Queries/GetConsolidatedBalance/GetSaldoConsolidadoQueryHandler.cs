using FinControl.Consolidation.Core.Domain;
using FinControl.Infrastructure.Cache;
using Microsoft.Extensions.Logging;

namespace FinControl.Consolidation.Core.Features.Queries.GetConsolidatedBalance;

public sealed class GetConsolidatedBalanceQueryHandler(
    RedisCacheService cache,
    ILogger<GetConsolidatedBalanceQueryHandler> logger)
{
    private const string CACHE_KEY_ACCUMULATED = "balance:consolidated:accumulated";
    private const int MAX_LOOKBACK_DAYS = 30;

    private static string CacheKey(DateOnly data) => $"balance:consolidated:{data:yyyy-MM-dd}";

    public async Task<GetConsolidatedBalanceResponse> Handle(
        GetConsolidatedBalanceQuery request,
        CancellationToken cancellationToken)
    {
        if (!request.TransactionDate.HasValue)
            return await GetAccumulatedAsync(cancellationToken);

        return await GetByDateAsync(request.TransactionDate.Value, cancellationToken);
    }

    private async Task<GetConsolidatedBalanceResponse> GetAccumulatedAsync(CancellationToken ct)
    {
        var saldo = await cache.GetAsync<ConsolidatedBalance>(CACHE_KEY_ACCUMULATED, ct);
        return new GetConsolidatedBalanceResponse(
            Balance: saldo?.Balance ?? 0,
            LastUpdated: saldo?.LastUpdated ?? DateTimeOffset.UtcNow);
    }

    private async Task<GetConsolidatedBalanceResponse> GetByDateAsync(
        DateOnly requestedDate,
        CancellationToken ct)
    {
        var requestedKey = CacheKey(requestedDate);
        var balance = await cache.GetAsync<ConsolidatedBalance>(requestedKey, ct);

        if (balance is null)
            balance = await FindPreviousBalanceAsync(requestedDate, requestedKey, ct);

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation(
                "Consolidated balance returned | Balance={Balance} LastUpdated={LastUpdated} CacheKey={CacheKey}",
                balance.Balance,
                balance.LastUpdated,
                requestedKey);

        return new GetConsolidatedBalanceResponse(balance.Balance, balance.LastUpdated);
    }

    private async Task<ConsolidatedBalance> FindPreviousBalanceAsync(
        DateOnly requestedDate,
        string requestedKey,
        CancellationToken ct)
    {
        for (int i = 1; i <= MAX_LOOKBACK_DAYS; i++)
        {
            var previousDate = requestedDate.AddDays(-i);
            var balance = await cache.GetAsync<ConsolidatedBalance>(CacheKey(previousDate), ct);

            if (balance is not null)
            {
                if (logger.IsEnabled(LogLevel.Information))
                    logger.LogInformation(
                        "Balance found on previous date | RequestedDate={RequestedDate} FoundDate={FoundDate} Attempt={Attempt}",
                        requestedDate, previousDate, i);

                // Propagate to requested date to speed up future queries for the same day
                await cache.SetAsync(requestedKey, balance, TimeSpan.FromDays(30), ct);
                return balance;
            }
        }

        logger.LogWarning(
            "No balance found in the last {MaxDays} days before {Date}. Returning zero balance.",
            MAX_LOOKBACK_DAYS, requestedDate);

        return new ConsolidatedBalance(0, DateTimeOffset.UtcNow);
    }
}

