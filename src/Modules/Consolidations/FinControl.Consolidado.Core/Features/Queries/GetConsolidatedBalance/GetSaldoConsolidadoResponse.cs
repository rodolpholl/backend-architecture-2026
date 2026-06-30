using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FinControl.Consolidated.Core.Features.Queries.GetConsolidatedBalance;

public record GetConsolidatedBalanceResponse(
    long Balance,
    DateTimeOffset LastUpdated
)
{
    public decimal BalanceDecimal => Balance / 100m;
}
