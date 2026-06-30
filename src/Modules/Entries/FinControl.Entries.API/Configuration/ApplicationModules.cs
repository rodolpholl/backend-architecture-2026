using Microsoft.AspNetCore.Builder;
using Wolverine.Http;

namespace FinControl.Entries.API.Configuration;

/// <summary>
/// Configuração centralizada de todos os módulos do projeto.
/// Padrão: cada módulo deve implementar extensões AddXyzModule e MapXyzModule.
/// </summary>
public static class ApplicationModules
{
    /// <summary>
    /// Registra todos os módulos de features da aplicação.
    /// Inclui Wolverine, handlers, validators e endpoints.
    /// </summary>
    public static WebApplicationBuilder AddAllModules(this WebApplicationBuilder builder)
    {
        // Módulo Entries - registra Wolverine, handlers, validators, endpoints
        builder.AddTransactionsModule();

        // TODO: Adicionar novos módulos aqui conforme forem criados
        // builder.AddXyzModule();

        return builder;
    }

    /// <summary>
    /// Mapeia middlewares e endpoints de todos os módulos.
    /// </summary>
    public static WebApplication MapAllModules(this WebApplication app)
    {
        // Mapeia todos os endpoints HTTP decorados com [WolverineGet], [WolverinePost], etc.
        app.MapWolverineEndpoints();

        // Módulo Entries - mapeia middlewares e endpoints
        app.MapTransactionsModule();

        // TODO: Mapear novos módulos aqui conforme forem criados
        // app.MapConsolidationModule();
        // app.MapXyzModule();

        return app;
    }
}

