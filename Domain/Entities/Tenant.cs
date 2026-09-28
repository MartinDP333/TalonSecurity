using System;
using System.Collections.Generic;

namespace Domain.Entities;

public class Tenant : BaseEntity
{
    public string Name { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Navigation properties
    public ICollection<ThreatLog> ThreatLogs { get; private set; } = new List<ThreatLog>();
    public ICollection<IpReputation> IpReputations { get; private set; } = new List<IpReputation>();
    public ICollection<AlertRule> AlertRules { get; private set; } = new List<AlertRule>();
    public ICollection<Notification> Notifications { get; private set; } = new List<Notification>();
    public ICollection<AutomatedResponseAction> AutomatedResponseActions { get; private set; } = new List<AutomatedResponseAction>();
    public ICollection<VulnerabilityReport> VulnerabilityReports { get; private set; } = new List<VulnerabilityReport>();

    private Tenant() { }

    public Tenant(string name)
    {
        Name = !string.IsNullOrWhiteSpace(name) ? name : throw new ArgumentException("Tenant skal have et navn", nameof(name));
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateName(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Navnet kan ikke være tomt", nameof(newName));
        Name = newName;
    }
}