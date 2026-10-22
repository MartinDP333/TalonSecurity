using System;
using System.Collections.Generic;

namespace Domain.Entities;

public class Tenant : BaseEntity
{
    public string Name { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public ICollection<ThreatLog> ThreatLogs { get; private set; } = new List<ThreatLog>();
    public ICollection<IpReputation> IpReputations { get; private set; } = new List<IpReputation>();
    public ICollection<AlertRule> AlertRules { get; private set; } = new List<AlertRule>();
    public ICollection<Notification> Notifications { get; private set; } = new List<Notification>();
    public ICollection<AutomatedResponseAction> AutomatedResponseActions { get; private set; } = new List<AutomatedResponseAction>();
    public ICollection<VulnerabilityReport> VulnerabilityReports { get; private set; } = new List<VulnerabilityReport>();
    public ICollection<SimulationScenario> SimulationScenarios { get; private set; } = new List<SimulationScenario>();
    public ICollection<TenantMembership> Memberships { get; private set; } = new List<TenantMembership>();

    private Tenant() { }
    public Tenant(Guid id, string name) { Id = id; Name = !string.IsNullOrWhiteSpace(name) ? name : throw new ArgumentException("Tenant name is required.", nameof(name)); CreatedAt = DateTime.UtcNow; }
    public Tenant(string name) : this(Guid.NewGuid(), name) { }
    public void UpdateName(string newName) { if (string.IsNullOrWhiteSpace(newName)) throw new ArgumentException("Name is required.", nameof(newName)); Name = newName; }
}
