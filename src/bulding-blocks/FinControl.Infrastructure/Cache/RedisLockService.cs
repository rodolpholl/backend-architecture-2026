using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace FinControl.Infrastructure.Cache;

/// <summary>
/// Implementação de lock distribuído usando Redis SET NX (SETNX semântico via StringSetAsync com When.NotExists).
/// Cada tentativa aguarda <c>retryDelay</c> antes de tentar novamente, até <c>maxAttempts</c> tentativas.
/// </summary>
public sealed class RedisLockService(
    IConnectionMultiplexer redis,
    ILogger<RedisLockService> logger) : IRedisLockService
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(100);

    public async Task<bool> ExecuteWithLockAsync(
        string lockKey,
        Func<Task> action,
        TimeSpan lockExpiry,
        CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        // Amount único por tentativa — impede que outro nó libere o lock deste nó
        var lockValue = $"{Environment.MachineName}:{Guid.NewGuid()}";

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            if (await db.StringSetAsync(lockKey, lockValue, lockExpiry, When.NotExists))
            {
                try
                {
                    await action();
                    return true;
                }
                finally
                {
                    // Libera apenas se ainda for o dono do lock (script Lua atômico)
                    const string releaseLua = """
                        if redis.call('get', KEYS[1]) == ARGV[1] then
                            return redis.call('del', KEYS[1])
                        else
                            return 0
                        end
                        """;
                    await db.ScriptEvaluateAsync(releaseLua, [(RedisKey)lockKey], [(RedisValue)lockValue]);
                }
            }

            if (attempt < MaxAttempts)
            {
                if (logger.IsEnabled(LogLevel.Debug))
                    logger.LogDebug(
                        "Lock '{LockKey}' ocupado (tentativa {Attempt}/{Max}), aguardando {Delay}ms...",
                        lockKey, attempt, MaxAttempts, RetryDelay.TotalMilliseconds);

                await Task.Delay(RetryDelay, ct);
            }
        }

        logger.LogWarning(
            "Nao foi possivel adquirir lock '{LockKey}' apos {Max} tentativas.",
            lockKey, MaxAttempts);

        return false;
    }
}
