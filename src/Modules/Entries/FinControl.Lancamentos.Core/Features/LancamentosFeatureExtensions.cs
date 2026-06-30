using FinControl.Infrastructure.Extensions;
using FinControl.Transactions.Core.Context;
using FinControl.Transactions.Core.Features.Commands.RegisterTransaction;
using FinControl.Transactions.Core.Outbox;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace FinControl.Transactions.Core.Features;

/// <summary>
/// Extensões de DI para registrar handlers, validators e endpoints do módulo Lancamentos.
/// 
/// Padrão: Esta extensão é chamada pelo ModuleExtensions do projeto .API.
/// Integração com Wolverine (auto-descoberta) + FluentValidation.
/// </summary>
public static class TransactionsFeatureExtensions
{
    /// <summary>
    /// Registra todos os handlers, validators e endpoints do módulo Lancamentos.
    /// 
    /// Wolverine usa descoberta por convenção (método "Handle" ou "HandleAsync").
    /// Validators são descobertos como AbstractValidator<T>.
    /// </summary>
    public static WebApplicationBuilder AddTransactionsFeatures(
        this WebApplicationBuilder builder)
    {
        // Registrar Wolverine com descoberta automática de handlers no assembly do Lancamentos.Core
        builder.AddFinControlWolverine<TransactionsDbContext>(
            configure: null,
            typeof(RegisterTransactionCommandHandler).Assembly
        );

        // Registrar validators FluentValidation (auto-descoberta por interface AbstractValidator<T>)
        builder.Services.AddValidatorsFromAssemblyContaining<RegisterTransactionCommandValidator>(
            lifetime: ServiceLifetime.Transient);

        // Relay service: lê mensagens pendentes do outbox e entrega ao RabbitMQ via Polly retry
        builder.Services.AddHostedService<OutboxRelayService>();

        return builder;
    }

    /// <summary>
    /// Mapeia os middlewares HTTP do FinControl (CorrelationId, ExceptionHandler).
    /// </summary>
    public static WebApplication MapTransactionsMiddleware(this WebApplication app)
    {
        app.UseFinControlMiddleware();
        return app;
    }
}
