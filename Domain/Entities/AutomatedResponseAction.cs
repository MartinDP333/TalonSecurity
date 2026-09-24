using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class AutomatedResponseAction : BaseEntity
    {
        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        public Guid ThreatLogId { get; set; }
        public ThreatLog ThreatLog { get; set; } = null!;

        public string ActionType { get; set; } = string.Empty;
        public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
        public bool IsSuccessful { get; set; }
    }
}
