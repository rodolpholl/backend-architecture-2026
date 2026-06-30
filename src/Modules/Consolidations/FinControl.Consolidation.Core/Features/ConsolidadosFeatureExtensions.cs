using FinControl.Consolidation.Core.Features.Queries.GetConsolidatedBalance;
using FinControl.Infrastructure.Extensions;
using Microsoft.AspNetCore.Builder;

namespace FinControl.Consolidation.Core.Features;

public static class ConsolidationFeatureExtensions
{
    public static WebApplicationBuilder AddConsolidationFeatures(
        this WebApplicationBuilder builder)
    {
        builder.AddFinControlRedis();

        builder.AddFinControlWolverine(
            configure: null,
            typeof(GetConsolidatedBalanceQueryHandler).Assembly);

        return builder;
    }

    public static WebApplication MapConsolidationMiddleware(this WebApplication app)
    {
        app.UseFinControlMiddleware();
        return app;
    }
}

