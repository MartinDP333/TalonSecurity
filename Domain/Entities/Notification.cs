using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class Notification : BaseEntity
    {
        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        public Guid? ThreatLogId { get; set; }
        public ThreatLog? ThreatLog { get; set; }

        public string Destination { get; set; } = string.Empty; // Webhook URL or Email
        public string Message { get; set; } = string.Empty;
        public bool IsSent { get; set; } = false;
        public DateTime? SentAt { get; set; }
    }
}
