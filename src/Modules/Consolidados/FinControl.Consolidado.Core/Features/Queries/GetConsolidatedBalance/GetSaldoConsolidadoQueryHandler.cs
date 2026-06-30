using FinControl.Consolidated.Core.Domain;
using FinControl.Infrastructure.Cache;
using Microsoft.Extensions.Logging;

namespace FinControl.Consolidated.Core.Features.Queries.GetConsolidatedBalance;

public sealed class GetConsolidatedBalanceQueryHandler(
    RedisCacheService cache,
    ILogger<GetConsolidatedBalanceQueryHandler> logger)
{
    private const string CACHE_KEY_ACUMULADO = "saldo:consolidado:acumulado";
    private const int MAX_LOOKBACK_DAYS = 30;

    private static string CacheKey(DateOnly data) => $"saldo:consolidado:{data:yyyy-MM-dd}";

    public async Task<GetConsolidatedBalanceResponse> Handle(
        GetConsolidatedBalanceQuery request,
        CancellationToken cancellationToken)
    {
        if (!request.TransactionDate.HasValue)
            return await GetAcumuladoAsync(cancellationToken);

        return await GetPorDataAsync(request.TransactionDate.Value, cancellationToken);
    }

    private async Task<GetConsolidatedBalanceResponse> GetAcumuladoAsync(CancellationToken ct)
    {
        var saldo = await cache.GetAsync<ConsolidatedBalance>(CACHE_KEY_ACUMULADO, ct);
        return new GetConsolidatedBalanceResponse(
            Balance: saldo?.Balance ?? 0,
            LastUpdated: saldo?.LastUpdated ?? DateTimeOffset.UtcNow);
    }

    private async Task<GetConsolidatedBalanceResponse> GetPorDataAsync(
        DateOnly dataRequisitada,
        CancellationToken ct)
    {
        var keyRequisitada = CacheKey(dataRequisitada);
        var saldo = await cache.GetAsync<ConsolidatedBalance>(keyRequisitada, ct);

        if (saldo is null)
            saldo = await BuscarSaldoAnteriorAsync(dataRequisitada, keyRequisitada, ct);

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation(
                "Saldo consolidado retornado | Saldo={Saldo} LastUpdated={LastUpdated} CacheKey={CacheKey}",
                saldo.Balance,
                saldo.LastUpdated,
                keyRequisitada);

        return new GetConsolidatedBalanceResponse(saldo.Balance, saldo.LastUpdated);
    }

    private async Task<ConsolidatedBalance> BuscarSaldoAnteriorAsync(
        DateOnly dataRequisitada,
        string keyRequisitada,
        CancellationToken ct)
    {
        for (int i = 1; i <= MAX_LOOKBACK_DAYS; i++)
        {
            var dataAnterior = dataRequisitada.AddDays(-i);
            var saldo = await cache.GetAsync<ConsolidatedBalance>(CacheKey(dataAnterior), ct);

            if (saldo is not null)
            {
                if (logger.IsEnabled(LogLevel.Information))
                    logger.LogInformation(
                        "Saldo encontrado em data anterior | DataRequisitada={DataRequisitada} DataEncontrada={DataEncontrada} Tentativa={Tentativa}",
                        dataRequisitada, dataAnterior, i);

                // Propaga para a data requisitada para acelerar consultas futuras ao mesmo dia
                await cache.SetAsync(keyRequisitada, saldo, TimeSpan.FromDays(30), ct);
                return saldo;
            }
        }

        logger.LogWarning(
            "Nenhum saldo encontrado nos últimos {MaxDias} dias anteriores a {Data}. Retornando saldo zero.",
            MAX_LOOKBACK_DAYS, dataRequisitada);

        return new ConsolidatedBalance(0, DateTimeOffset.UtcNow);
    }
}
