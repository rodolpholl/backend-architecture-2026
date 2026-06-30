using FinControl.Infrastructure.Messaging;
using FinControl.Infrastructure.Middleware;
using FinControl.Infrastructure.Vault;
using FinControl.Infrastructure.Wolverine;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Http;
using Wolverine.RabbitMQ;

namespace FinControl.Infrastructure.Extensions;

/// <summary>
/// Central extension method to register Wolverine as mediator + bus + outbox.
///
/// Wolverine unifies 3 responsibilities without additional dependencies:
///   1. In-process mediator (replaces MediatR) — via IMessageBus.InvokeAsync()
///   2. Distributed Message Bus — publishes/consumes from RabbitMQ
///   3. Native Outbox — persists messages in the same EF Core transaction
///
/// Handler pattern by CONVENTION (no IRequestHandler interface):
///
///   public sealed class MyCommandHandler
///   {
///       public async Task&lt;Result&gt; Handle(MyCommand cmd, MyDbContext db, CancellationToken ct)
///       { ... }
///   }
///
/// Wolverine discovers the handler by reflection in the provided assembly.
/// Outbox works automatically when UseWolverineMessagestore() is configured.
/// </summary>
public static class WolverineExtensions
{
    /// <summary>
    /// Registers Wolverine with:
    ///   - Global logging and validation middlewares
    ///   - Native Outbox via EF Core (WolverineFx.EntityFrameworkCore)
    ///   - RabbitMQ as distributed transport
    ///   - Handler discovery in provided assemblies
    /// </summary>
    public static WebApplicationBuilder AddFinControlWolverine<TDbContext>(
        this WebApplicationBuilder builder,
        Action<WolverineOptions>? configure = null,
        params System.Reflection.Assembly[] handlerAssemblies)
        where TDbContext : DbContext
    {
        // VaultKeys.RabbitMqUri → "rabbitmq:uri" (Vault path: dev/rabbitmq → uri)
        var rabbitMqUri = builder.Configuration[VaultKeys.RabbitMqUri];
        
        // In development, RabbitMQ is optional
        var isRabbitMqAvailable = !string.IsNullOrEmpty(rabbitMqUri);

        builder.Services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
        builder.Services.AddWolverineHttp();

        builder.Host.UseWolverine(opts =>
        {
            // ── Global middlewares (order matters) ───────────────────────────
            // 1. Logging FIRST: captures total latency including rejected validations
            // 2. Validation THEN: validates and throws ValidationException before handler
            opts.Policies.AddMiddleware<LoggingMiddleware>();
            opts.Policies.AddMiddleware<FluentValidationMiddleware>();

            // ── Native Outbox via EF Core ──────────────────────────────────────
            // Wolverine intercepts SaveChangesAsync() and wraps published messages
            // within the same database transaction — without manual Outbox table/service.
            opts.UseEntityFrameworkCoreTransactions();

            // ── RabbitMQ transport (only if available) ───────────────────────
            if (isRabbitMqAvailable)
            {
                opts.UseRabbitMq(new Uri(rabbitMqUri!))
                    .AutoProvision();
            }
            else if (!builder.Environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    $"Secret '{VaultKeys.RabbitMqUri}' not found in Vault (dev/rabbitmq → uri). " +
                    "RabbitMQ is required in production.");
            }
            // In development, uses only in-process mediator (no RabbitMQ)

            // ── Handler discovery ─────────────────────────────────────────
            // Wolverine scans the assemblies for Handle/HandleAsync methods
            // by convention — without needing to manually register each handler.
            foreach (var assembly in handlerAssemblies)
                opts.Discovery.IncludeAssembly(assembly);

            // ── Module-specific configurations ────────────────────────
            configure?.Invoke(opts);
        });

        if (builder.Environment.IsDevelopment() && !isRabbitMqAvailable)
        {
            Console.WriteLine("ℹ️  RabbitMQ not configured - Wolverine will run only in in-process mode (no distribution)");
        }

        return builder;
    }

    /// <summary>
    /// Overload without EF Core — for modules that use only Redis/cache and don't have DbContext.
    /// Identical to the generic, but without <c>UseEntityFrameworkCoreTransactions()</c>.
    /// </summary>
    public static WebApplicationBuilder AddFinControlWolverine(
        this WebApplicationBuilder builder,
        Action<WolverineOptions>? configure = null,
        params System.Reflection.Assembly[] handlerAssemblies)
    {
        var rabbitMqUri = builder.Configuration[VaultKeys.RabbitMqUri];
        var isRabbitMqAvailable = !string.IsNullOrEmpty(rabbitMqUri);

        builder.Services.AddWolverineHttp();

        builder.Host.UseWolverine(opts =>
        {
            opts.Policies.AddMiddleware<LoggingMiddleware>();
            opts.Policies.AddMiddleware<FluentValidationMiddleware>();

            if (isRabbitMqAvailable)
            {
                opts.UseRabbitMq(new Uri(rabbitMqUri!))
                    .AutoProvision();
            }
            else if (!builder.Environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    $"Secret '{VaultKeys.RabbitMqUri}' not found in Vault (dev/rabbitmq → uri). " +
                    "RabbitMQ is required in production.");
            }

            foreach (var assembly in handlerAssemblies)
                opts.Discovery.IncludeAssembly(assembly);

            configure?.Invoke(opts);
        });

        if (builder.Environment.IsDevelopment() && !isRabbitMqAvailable)
        {
            Console.WriteLine("ℹ️  RabbitMQ not configured - Wolverine will run only in in-process mode (no distribution)");
        }

        return builder;
    }

    /// <summary>
    /// Registers standard FinControl HTTP middlewares:
    ///   - CorrelationId (extrai/gera X-Correlation-Id)
    ///   - ExceptionHandler global (ProblemDetails RFC 7807)
    /// </summary>
    public static WebApplication UseFinControlMiddleware(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseExceptionHandler();
        return app;
    }
}
