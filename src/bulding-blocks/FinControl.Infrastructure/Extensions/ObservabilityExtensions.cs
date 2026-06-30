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
/// Configura a stack completa de observabilidade:
///   - OpenTelemetry Traces → exporta via OTLP (Jaeger / Grafana Tempo)
///   - Prometheus Metrics  → expõe /metrics para scraping
///
/// Uso no Program.cs do módulo:
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
                               $"Secret '{VaultKeys.OtlpEndpoint}' não encontrado no Vault (dev/grafana → otlp_endpoint). " +
                               "Configure o Vault antes de inicializar a observabilidade.");

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
                    // Ignora health checks nos traces
                    opts.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health");
                    opts.RecordException = true;
                })
                .AddHttpClientInstrumentation(opts => opts.RecordException = true)
                .AddEntityFrameworkCoreInstrumentation(opts =>
                {
                    // SetDbStatementForText foi removido na 1.15.x.
                    // O SQL statement (db.statement) é capturado por padrão.
                    // Use EnrichWithIDbCommand para adicionar atributos customizados ao span.
                    opts.EnrichWithIDbCommand = (activity, command) =>
                    {
                        activity.SetTag("db.command_type", command.CommandType.ToString());
                    };
                })
                .AddOtlpExporter(opts => opts.Endpoint = new Uri(otlpEndpoint)));

        return builder;
    }

    /// <summary>
    /// Expõe o endpoint /metrics para scraping pelo Prometheus.
    /// Registrar ANTES de MapControllers/MapEndpoints.
    /// </summary>
    public static IApplicationBuilder UseFinControlObservability(this IApplicationBuilder app)
    {
        // Coleta métricas padrão .NET (GC, threads, process)
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
    /// Mapeia o endpoint /metrics do Prometheus.
    /// Chamar em app.MapFinControlMetricsEndpoint() após UseRouting.
    /// </summary>
    public static IEndpointRouteBuilder MapFinControlMetricsEndpoint(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMetrics("/metrics");
        return endpoints;
    }
}
