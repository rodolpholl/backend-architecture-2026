using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FinControl.Consolidation.Core.Domain;

public record ConsolidatedBalance(
    long Balance,
    DateTimeOffset LastUpdated
);
