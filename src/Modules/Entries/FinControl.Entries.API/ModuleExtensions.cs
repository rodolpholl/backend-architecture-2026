using FinControl.Entries.Core.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace FinControl.Entries.API;

/// <summary>
/// Extensões de DI e configuração para o módulo Entries.
/// Responsável por registrar todos os handlers, validators, endpoints e middlewares.
/// </summary>
public static class ModuleExtensions
{
    /// <summary>
    /// Registra todos os serviços, handlers e endpoints do módulo Entries.
    /// </summary>
    public static WebApplicationBuilder AddTransactionsModule(this WebApplicationBuilder builder)
    {
        // Registra Wolverine, validators e descoberta de handlers
        builder.AddTransactionsFeatures();
        return builder;
    }

    /// <summary>
    /// Mapeia middlewares e endpoints do módulo Entries.
    /// </summary>
    public static WebApplication MapTransactionsModule(this WebApplication app)
    {
        // Registra middlewares HTTP padrão (CorrelationId, ExceptionHandler, etc)
        app.MapTransactionsMiddleware();
        return app;
    }
}

