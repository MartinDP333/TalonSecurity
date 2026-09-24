using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class Tenant : BaseEntity
    {
        public string Name { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navigation properties for relationships
        public ICollection<ThreatLog> ThreatLogs { get; set; } = new List<ThreatLog>();
        public ICollection<IpReputation> IpReputations { get; set; } = new List<IpReputation>();
        public ICollection<AlertRule> AlertRules { get; set; } = new List<AlertRule>();
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        public ICollection<AutomatedResponseAction> AutomatedResponseActions { get; set; } = new List<AutomatedResponseAction>();
        public ICollection<VulnerabilityReport> VulnerabilityReports { get; set; } = new List<VulnerabilityReport>();
    }

}
