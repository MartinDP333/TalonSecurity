using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class ThreatLog : BaseEntity
    {
        public Guid TenantId { get; set; } required
        public Tenant Tenant { get; set; } required
        public string SourceIp { get; set; } = string.Empty;
        public string RequestPath { get; set; } = string.Empty;
        public string AttackType { get; set; } = string.Empty;
        public SeverityLevel Severity { get; set; }
        public string RawPayload { get; set; } = string.Empty;
        public string CountryCode { get; set; } = string.Empty;
        public int HitCount { get; set; } = 1;
        public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;
        public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Notification? Notification { get; set; }
        public AutomatedResponseAction? ResponseAction { get; set; }

    }
}
