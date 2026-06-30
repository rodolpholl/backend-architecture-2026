using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;
using Wolverine.Http;

namespace FinControl.Consolidated.Core.Features.Queries.GetConsolidatedBalance;

public class GetConsolidatedBalanceEndpoint
{
    [Authorize]
    [WolverineGet("/consolidados/saldo")]
    public static async Task<GetConsolidatedBalanceResponse> Handle(
        [FromQuery(Name = "transaction-date")] DateOnly? transactionDate,
        IMessageBus bus,
        CancellationToken cancellationToken = default)
    {
        var query = new GetConsolidatedBalanceQuery(transactionDate);
        return await bus.InvokeAsync<GetConsolidatedBalanceResponse>(query, cancellationToken);
    }
}
