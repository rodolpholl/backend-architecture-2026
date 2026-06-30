using Microsoft.AspNetCore.Builder;
using Wolverine.Http;

namespace FinControl.Consolidation.API.Configuration;

public static class ApplicationModules
{
    public static WebApplicationBuilder AddAllModules(this WebApplicationBuilder builder)
    {
        builder.AddConsolidationModule();
        return builder;
    }

    public static WebApplication MapAllModules(this WebApplication app)
    {
        app.MapWolverineEndpoints();
        app.MapConsolidationModule();
        return app;
    }
}

