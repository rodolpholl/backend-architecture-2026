using FinControl.Infrastructure.Cache;
using FinControl.Infrastructure.Vault;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace FinControl.Infrastructure.Extensions;

/// <summary>
/// Registra o Redis como IDistributedCache com a connection string proveniente do Vault.
///
/// A connection string deve estar no Vault no path configurado,
/// com chave: ConnectionStrings__Redis
/// (o provider normaliza "__" → ":" no IConfiguration).
///
/// Uso:
///   builder.AddFinControlRedis();
///   // registra IDistributedCache (Redis) + RedisCacheService
/// </summary>
public static class RedisExtensions
{
    public static WebApplicationBuilder AddFinControlRedis(this WebApplicationBuilder builder)
    {
        // VaultKeys.RedisConnection → "redis:connection_string" (Vault path: dev/redis → connection_string)
        var connectionString = builder.Configuration[VaultKeys.RedisConnection]
            ?? throw new InvalidOperationException(
                $"Secret '{VaultKeys.RedisConnection}' não encontrado no Vault (dev/redis → connection_string). " +
                "Certifique-se de que o Vault está configurado antes de registrar o Redis.");

        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = connectionString;
            options.InstanceName = "FinControl:";
        });

        // IConnectionMultiplexer compartilhado: usado por RedisCacheService e IRedisLockService
        builder.Services.AddSingleton<IConnectionMultiplexer>(
            _ => ConnectionMultiplexer.Connect(connectionString));

        builder.Services.AddSingleton<RedisCacheService>();
        builder.Services.AddSingleton<IRedisLockService, RedisLockService>();

        return builder;
    }
}
