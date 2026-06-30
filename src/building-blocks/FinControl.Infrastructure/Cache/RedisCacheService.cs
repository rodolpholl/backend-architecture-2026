using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FinControl.Infrastructure.Cache;

/// <summary>
/// Wrapper tipado sobre IDistributedCache (Redis).
/// Usa System.Text.Json para serializacao.
/// </summary>
public sealed class RedisCacheService(
    IDistributedCache cache,
    ILogger<RedisCacheService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var bytes = await cache.GetAsync(key, ct);
        if (bytes is null)
        {
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("Cache MISS | Key: {Key}", key);
            return default;
        }

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("Cache HIT  | Key: {Key}", key);

        return JsonSerializer.Deserialize<T>(bytes, JsonOptions);
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? absoluteExpiration = null,
        CancellationToken ct = default)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = absoluteExpiration ?? TimeSpan.FromHours(24)
        };

        await cache.SetAsync(key, bytes, options, ct);

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("Cache SET  | Key: {Key} | TTL: {TTL}", key, options.AbsoluteExpirationRelativeToNow);
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        await cache.RemoveAsync(key, ct);

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("Cache DEL  | Key: {Key}", key);
    }

    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? absoluteExpiration = null,
        CancellationToken ct = default)
    {
        var cached = await GetAsync<T>(key, ct);
        if (cached is not null) return cached;

        var value = await factory(ct);
        await SetAsync(key, value, absoluteExpiration, ct);
        return value;
    }
}
