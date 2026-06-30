using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FinControl.Consolidated.Core.Domain;

public record ConsolidatedBalance(
    long Balance,
    DateTimeOffset LastUpdated
);