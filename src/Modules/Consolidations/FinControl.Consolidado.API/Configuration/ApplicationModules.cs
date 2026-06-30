using Microsoft.AspNetCore.Builder;
using Wolverine.Http;

namespace FinControl.Consolidated.API.Configuration;

public static class ApplicationModules
{
    public static WebApplicationBuilder AddAllModules(this WebApplicationBuilder builder)
    {
        builder.AddConsolidadosModule();
        return builder;
    }

    public static WebApplication MapAllModules(this WebApplication app)
    {
        app.MapWolverineEndpoints();
        app.MapConsolidadosModule();
        return app;
    }
}
