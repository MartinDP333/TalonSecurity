using System;

namespace Domain.Entities;

public class Notification : BaseEntity
{
    public Guid TenantId { get; private set; }
    public Tenant Tenant { get; private set; } = null!;
    public Guid? ThreatLogId { get; private set; }
    public ThreatLog? ThreatLog { get; private set; }
    public string Destination { get; private set; }
    public string Message { get; private set; }
    public bool IsSent { get; private set; }
    public DateTime? SentAt { get; private set; }

    private Notification() { }

    public Notification(Guid tenantId, string destination, string message, Guid? threatLogId = null)
    {
        TenantId = tenantId != Guid.Empty ? tenantId : throw new ArgumentException("Ugyldig TenantId", nameof(tenantId));
        Destination = !string.IsNullOrWhiteSpace(destination) ? destination : throw new ArgumentException("Destination skal angives", nameof(destination));
        Message = !string.IsNullOrWhiteSpace(message) ? message : throw new ArgumentException("Besked kan ikke være tom", nameof(message));
        ThreatLogId = threatLogId;
        IsSent = false;
    }

    public void MarkAsSent()
    {
        IsSent = true;
        SentAt = DateTime.UtcNow;
    }
}