using FinControl.Infrastructure.Vault;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using HealthChecks.UI.Client;

namespace FinControl.Infrastructure.Extensions;

/// <summary>
/// Configures health checks for PostgreSQL, Redis, and RabbitMQ.
/// Exposes /health (liveness) and /health/ready (readiness).
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
                        $"Secret '{VaultKeys.PostgresConnection}' not found in Vault (dev/postgres → connection_string)."),
                name: "postgres",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["db", "postgres", "ready"]);

        if (includeRedis)
        {
            hc.AddRedis(
                configuration[VaultKeys.RedisConnection]
                    ?? throw new InvalidOperationException(
                        $"Secret '{VaultKeys.RedisConnection}' not found in Vault (dev/redis → connection_string)."),
                name: "redis",
                failureStatus: HealthStatus.Degraded,
                tags: ["cache", "redis", "ready"]);
        }

        if (includeRabbitMq)
        {
            var rabbitMqUri = configuration[VaultKeys.RabbitMqUri]
                ?? throw new InvalidOperationException(
                    $"Secret '{VaultKeys.RabbitMqUri}' not found in Vault (dev/rabbitmq → uri).");

            hc.AddRabbitMQ(
                sp => new RabbitMQ.Client.ConnectionFactory { Uri = new Uri(rabbitMqUri) }.CreateConnectionAsync(),
                name: "rabbitmq",
                failureStatus: HealthStatus.Degraded,
                tags: ["bus", "rabbitmq", "ready"]);
        }

        return services;
    }

    /// <summary>
    /// Maps /health and /health/ready with detailed JSON response.
    /// </summary>
    public static IEndpointRouteBuilder MapFinControlHealthChecks(
        this IEndpointRouteBuilder endpoints)
    {
        // Liveness: only indicates if the process is alive
        endpoints.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });

        // Readiness: executes all checks with "ready" tag
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });

        return endpoints;
    }
}
