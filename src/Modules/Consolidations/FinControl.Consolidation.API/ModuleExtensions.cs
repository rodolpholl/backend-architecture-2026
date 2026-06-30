using FinControl.Consolidation.Core.Features;
using Microsoft.AspNetCore.Builder;

namespace FinControl.Consolidation.API;

public static class ModuleExtensions
{
    public static WebApplicationBuilder AddConsolidationModule(this WebApplicationBuilder builder)
    {
        builder.AddConsolidationFeatures();
        return builder;
    }

    public static WebApplication MapConsolidationModule(this WebApplication app)
    {
        app.MapConsolidationMiddleware();
        return app;
    }
}

