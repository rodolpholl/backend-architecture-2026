using FinControl.Infrastructure.Cache;
using FinControl.Infrastructure.Vault;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace FinControl.Infrastructure.Extensions;

/// <summary>
/// Registers Redis as IDistributedCache with connection string from Vault.
///
/// The connection string must be in Vault in the configured path,
/// with key: ConnectionStrings__Redis
/// (the provider normalizes "__" → ":" in IConfiguration).
///
/// Usage:
///   builder.AddFinControlRedis();
///   // registers IDistributedCache (Redis) + RedisCacheService
/// </summary>
public static class RedisExtensions
{
    public static WebApplicationBuilder AddFinControlRedis(this WebApplicationBuilder builder)
    {
        // VaultKeys.RedisConnection → "redis:connection_string" (Vault path: dev/redis → connection_string)
        var connectionString = builder.Configuration[VaultKeys.RedisConnection]
            ?? throw new InvalidOperationException(
                $"Secret '{VaultKeys.RedisConnection}' not found in Vault (dev/redis → connection_string). " +
                "Ensure that Vault is configured before registering Redis.");

        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = connectionString;
            options.InstanceName = "FinControl:";
        });

        // Shared IConnectionMultiplexer: used by RedisCacheService and IRedisLockService
        builder.Services.AddSingleton<IConnectionMultiplexer>(
            _ => ConnectionMultiplexer.Connect(connectionString));

        builder.Services.AddSingleton<RedisCacheService>();
        builder.Services.AddSingleton<IRedisLockService, RedisLockService>();

        return builder;
    }
}
