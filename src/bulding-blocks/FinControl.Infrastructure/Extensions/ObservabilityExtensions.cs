using FinControl.Infrastructure.Vault;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using Prometheus;

namespace FinControl.Infrastructure.Extensions;

/// <summary>
/// Configures the complete observability stack:
///   - OpenTelemetry Traces → exports via OTLP (Jaeger / Grafana Tempo)
///   - Prometheus Metrics  → exposes /metrics for scraping
///
/// Usage in module's Program.cs:
///   builder.AddFinControlObservability("fincontrol-lancamentos");
///   app.UseFinControlObservability();
/// </summary>
public static class ObservabilityExtensions
{
    public static WebApplicationBuilder AddFinControlObservability(
        this WebApplicationBuilder builder,
        string serviceName)
    {
        // VaultKeys.OtlpEndpoint → "grafana:otlp_endpoint" (Vault path: dev/grafana → otlp_endpoint)
        var otlpEndpoint = builder.Configuration[VaultKeys.OtlpEndpoint]
                           ?? throw new InvalidOperationException(
                               $"Secret '{VaultKeys.OtlpEndpoint}' not found in Vault (dev/grafana → otlp_endpoint). " +
                               "Configure Vault before initializing observability.");

        // --- OpenTelemetry Traces ---
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r
                .AddService(serviceName)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = builder.Environment.EnvironmentName
                }))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(opts =>
                {
                    // Ignores health checks in traces
                    opts.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health");
                    opts.RecordException = true;
                })
                .AddHttpClientInstrumentation(opts => opts.RecordException = true)
                .AddEntityFrameworkCoreInstrumentation(opts =>
                {
                    // SetDbStatementForText was removed in 1.15.x.
                    // SQL statement (db.statement) is captured by default.
                    // Use EnrichWithIDbCommand to add custom attributes to the span.
                    opts.EnrichWithIDbCommand = (activity, command) =>
                    {
                        activity.SetTag("db.command_type", command.CommandType.ToString());
                    };
                })
                .AddOtlpExporter(opts => opts.Endpoint = new Uri(otlpEndpoint)));

        return builder;
    }

    /// <summary>
    /// Exposes the /metrics endpoint for scraping by Prometheus.
    /// Register BEFORE MapControllers/MapEndpoints.
    /// </summary>
    public static IApplicationBuilder UseFinControlObservability(this IApplicationBuilder app)
    {
        // Collects standard .NET metrics (GC, threads, process)
        app.UseHttpMetrics(options =>
        {
            options.AddCustomLabel("service", ctx =>
                ctx.RequestServices
                    .GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>()
                    .ApplicationName);
        });

        return app;
    }

    /// <summary>
    /// Maps the Prometheus /metrics endpoint.
    /// Call in app.MapFinControlMetricsEndpoint() after UseRouting.
    /// </summary>
    public static IEndpointRouteBuilder MapFinControlMetricsEndpoint(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMetrics("/metrics");
        return endpoints;
    }
}
