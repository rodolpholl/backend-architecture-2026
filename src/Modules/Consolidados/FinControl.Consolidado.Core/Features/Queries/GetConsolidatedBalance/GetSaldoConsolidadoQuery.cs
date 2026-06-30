using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FinControl.SharedKernel.Messaging;

namespace FinControl.Consolidated.Core.Features.Queries.GetConsolidatedBalance;

public record GetConsolidatedBalanceQuery(DateOnly? TransactionDate = null) : IQuery<GetConsolidatedBalanceResponse>;
