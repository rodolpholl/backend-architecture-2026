using FinControl.Consolidated.Core.Features;
using Microsoft.AspNetCore.Builder;

namespace FinControl.Consolidated.API;

public static class ModuleExtensions
{
    public static WebApplicationBuilder AddConsolidadosModule(this WebApplicationBuilder builder)
    {
        builder.AddConsolidadosFeatures();
        return builder;
    }

    public static WebApplication MapConsolidadosModule(this WebApplication app)
    {
        app.MapConsolidadosMiddleware();
        return app;
    }
}
