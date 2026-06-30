using FinControl.Infrastructure.Vault;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using HealthChecks.UI.Client;

namespace FinControl.Infrastructure.Extensions;

/// <summary>
/// Configura health checks para PostgreSQL, Redis e RabbitMQ.
/// Expoe /health (liveness) e /health/ready (readiness).
/// </summary>
public static class HealthChecksExtensions
{
    public static IServiceCollection AddFinControlHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration,
        bool includeRedis    = true,
        bool includeRabbitMq = true)
    {
        // VaultKeys.PostgresConnection → "postgres:connection_string" (Vault path: dev/postgres)
        // VaultKeys.RedisConnection    → "redis:connection_string"    (Vault path: dev/redis)
        // VaultKeys.RabbitMqUri        → "rabbitmq:uri"               (Vault path: dev/rabbitmq)
        var hc = services
            .AddHealthChecks()
            .AddNpgSql(
                configuration[VaultKeys.PostgresConnection]
                    ?? throw new InvalidOperationException(
                        $"Secret '{VaultKeys.PostgresConnection}' não encontrado no Vault (dev/postgres → connection_string)."),
                name: "postgres",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["db", "postgres", "ready"]);

        if (includeRedis)
        {
            hc.AddRedis(
                configuration[VaultKeys.RedisConnection]
                    ?? throw new InvalidOperationException(
                        $"Secret '{VaultKeys.RedisConnection}' não encontrado no Vault (dev/redis → connection_string)."),
                name: "redis",
                failureStatus: HealthStatus.Degraded,
                tags: ["cache", "redis", "ready"]);
        }

        if (includeRabbitMq)
        {
            var rabbitMqUri = configuration[VaultKeys.RabbitMqUri]
                ?? throw new InvalidOperationException(
                    $"Secret '{VaultKeys.RabbitMqUri}' não encontrado no Vault (dev/rabbitmq → uri).");

            hc.AddRabbitMQ(
                sp => new RabbitMQ.Client.ConnectionFactory { Uri = new Uri(rabbitMqUri) }.CreateConnectionAsync(),
                name: "rabbitmq",
                failureStatus: HealthStatus.Degraded,
                tags: ["bus", "rabbitmq", "ready"]);
        }

        return services;
    }

    /// <summary>
    /// Mapeia /health e /health/ready com resposta JSON detalhada.
    /// </summary>
    public static IEndpointRouteBuilder MapFinControlHealthChecks(
        this IEndpointRouteBuilder endpoints)
    {
        // Liveness: apenas indica se o processo esta vivo
        endpoints.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });

        // Readiness: executa todos os checks com tag "ready"
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });

        return endpoints;
    }
}
