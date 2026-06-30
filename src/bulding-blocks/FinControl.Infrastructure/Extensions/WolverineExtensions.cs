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
/// Extension method central para registrar Wolverine como mediator + bus + outbox.
///
/// Wolverine unifica 3 responsabilidades sem dependências adicionais:
///   1. Mediador in-process (substitui MediatR) — via IMessageBus.InvokeAsync()
///   2. Message Bus distribuído — publica/consome do RabbitMQ
///   3. Outbox nativo — persiste mensagens na mesma transação EF Core
///
/// Padrão de handler por CONVENÇÃO (sem interface IRequestHandler):
///
///   public sealed class MinhaCommandHandler
///   {
///       public async Task&lt;Result&gt; Handle(MinhaCommand cmd, MeuDbContext db, CancellationToken ct)
///       { ... }
///   }
///
/// O Wolverine descobre o handler por reflexão no assembly informado.
/// O Outbox funciona automaticamente quando UseWolverineMessagestore() é configurado.
/// </summary>
public static class WolverineExtensions
{
    /// <summary>
    /// Registra Wolverine com:
    ///   - Logging e Validation middlewares globais
    ///   - Outbox nativo via EF Core (WolverineFx.EntityFrameworkCore)
    ///   - RabbitMQ como transport distribuído
    ///   - Descoberta de handlers nos assemblies informados
    /// </summary>
    public static WebApplicationBuilder AddFinControlWolverine<TDbContext>(
        this WebApplicationBuilder builder,
        Action<WolverineOptions>? configure = null,
        params System.Reflection.Assembly[] handlerAssemblies)
        where TDbContext : DbContext
    {
        // VaultKeys.RabbitMqUri → "rabbitmq:uri" (Vault path: dev/rabbitmq → uri)
        var rabbitMqUri = builder.Configuration[VaultKeys.RabbitMqUri];
        
        // Em desenvolvimento, RabbitMQ é opcional
        var isRabbitMqAvailable = !string.IsNullOrEmpty(rabbitMqUri);

        builder.Services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
        builder.Services.AddWolverineHttp();

        builder.Host.UseWolverine(opts =>
        {
            // ── Middlewares globais (ordem importa) ───────────────────────────
            // 1. Logging ANTES: captura latência total inclusive de validações rejeitadas
            // 2. Validation DEPOIS: valida e lança ValidationException antes do handler
            opts.Policies.AddMiddleware<LoggingMiddleware>();
            opts.Policies.AddMiddleware<FluentValidationMiddleware>();

            // ── Outbox nativo via EF Core ──────────────────────────────────────
            // Wolverine intercepta SaveChangesAsync() e envolve mensagens publicadas
            // dentro da mesma transação do banco — sem tabela/serviço manual de Outbox.
            opts.UseEntityFrameworkCoreTransactions();

            // ── RabbitMQ transport (apenas se disponível) ───────────────────────
            if (isRabbitMqAvailable)
            {
                opts.UseRabbitMq(new Uri(rabbitMqUri!))
                    .AutoProvision();
            }
            else if (!builder.Environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    $"Secret '{VaultKeys.RabbitMqUri}' não encontrado no Vault (dev/rabbitmq → uri). " +
                    "RabbitMQ é obrigatório em produção.");
            }
            // Em desenvolvimento, usa apenas mediator in-process (sem RabbitMQ)

            // ── Descoberta de handlers ─────────────────────────────────────────
            // Wolverine varre os assemblies procurando métodos Handle/HandleAsync
            // por convenção — sem necessidade de registrar manualmente cada handler.
            foreach (var assembly in handlerAssemblies)
                opts.Discovery.IncludeAssembly(assembly);

            // ── Configurações adicionais do módulo ────────────────────────────
            configure?.Invoke(opts);
        });

        if (builder.Environment.IsDevelopment() && !isRabbitMqAvailable)
        {
            Console.WriteLine("ℹ️  RabbitMQ não configurado - Wolverine rodará apenas em modo in-process (sem distribuição)");
        }

        return builder;
    }

    /// <summary>
    /// Overload sem EF Core — para módulos que usam apenas Redis/cache e não possuem DbContext.
    /// Idêntico ao genérico, mas sem <c>UseEntityFrameworkCoreTransactions()</c>.
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
                    $"Secret '{VaultKeys.RabbitMqUri}' não encontrado no Vault (dev/rabbitmq → uri). " +
                    "RabbitMQ é obrigatório em produção.");
            }

            foreach (var assembly in handlerAssemblies)
                opts.Discovery.IncludeAssembly(assembly);

            configure?.Invoke(opts);
        });

        if (builder.Environment.IsDevelopment() && !isRabbitMqAvailable)
        {
            Console.WriteLine("ℹ️  RabbitMQ não configurado - Wolverine rodará apenas em modo in-process (sem distribuição)");
        }

        return builder;
    }

    /// <summary>
    /// Registra middlewares HTTP padrão do FinControl:
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
