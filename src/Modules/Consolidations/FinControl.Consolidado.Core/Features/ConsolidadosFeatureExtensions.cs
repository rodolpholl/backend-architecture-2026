using FinControl.Consolidated.Core.Features.Queries.GetConsolidatedBalance;
using FinControl.Infrastructure.Extensions;
using Microsoft.AspNetCore.Builder;

namespace FinControl.Consolidated.Core.Features;

public static class ConsolidadosFeatureExtensions
{
    public static WebApplicationBuilder AddConsolidadosFeatures(
        this WebApplicationBuilder builder)
    {
        builder.AddFinControlRedis();

        builder.AddFinControlWolverine(
            configure: null,
            typeof(GetConsolidatedBalanceQueryHandler).Assembly);

        return builder;
    }

    public static WebApplication MapConsolidadosMiddleware(this WebApplication app)
    {
        app.UseFinControlMiddleware();
        return app;
    }
}
