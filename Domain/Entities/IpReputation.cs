using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class IpReputation : BaseEntity
    {
        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        public string IpAddress { get; set; } = string.Empty;
        public int RiskScore { get; set; } // 0 to 100
        public int TotalIncidents { get; set; }
        public bool IsBlocked { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    }
}
