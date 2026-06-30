using FinControl.Transactions.Core.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace FinControl.Transactions.API;

/// <summary>
/// Extensões de DI e configuração para o módulo Lancamentos.
/// Responsável por registrar todos os handlers, validators, endpoints e middlewares.
/// </summary>
public static class ModuleExtensions
{
    /// <summary>
    /// Registra todos os serviços, handlers e endpoints do módulo Lancamentos.
    /// </summary>
    public static WebApplicationBuilder AddTransactionsModule(this WebApplicationBuilder builder)
    {
        // Registra Wolverine, validators e descoberta de handlers
        builder.AddTransactionsFeatures();
        return builder;
    }

    /// <summary>
    /// Mapeia middlewares e endpoints do módulo Lancamentos.
    /// </summary>
    public static WebApplication MapTransactionsModule(this WebApplication app)
    {
        // Registra middlewares HTTP padrão (CorrelationId, ExceptionHandler, etc)
        app.MapTransactionsMiddleware();
        return app;
    }
}
