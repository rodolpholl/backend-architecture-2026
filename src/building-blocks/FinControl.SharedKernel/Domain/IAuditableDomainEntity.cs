using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FinControl.SharedKernel.Domain
{
    public interface IAuditableDomainEntity
    {
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public string CreatedBy { get; set; }
        public string CreatedByName { get; set; }
        public string CreatedByEmail { get; set; }
        public string? UpdatedBy { get; set; }
        public string? UpdatedByName { get; set; }
        public string? UpdatedByEmail { get; set; }
    }
}