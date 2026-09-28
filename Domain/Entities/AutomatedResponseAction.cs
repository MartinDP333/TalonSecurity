using System;

namespace Domain.Entities;

public class AutomatedResponseAction : BaseEntity
{
    public Guid TenantId { get; private set; }
    public Tenant Tenant { get; private set; } = null!;
    public Guid ThreatLogId { get; private set; }
    public ThreatLog ThreatLog { get; private set; } = null!;
    public string ActionType { get; private set; }
    public DateTime ExecutedAt { get; private set; }
    public bool IsSuccessful { get; private set; }

    private AutomatedResponseAction() { }

    public AutomatedResponseAction(Guid tenantId, Guid threatLogId, string actionType, bool isSuccessful)
    {
        TenantId = tenantId != Guid.Empty ? tenantId : throw new ArgumentException("Ugyldig TenantId", nameof(tenantId));
        ThreatLogId = threatLogId != Guid.Empty ? threatLogId : throw new ArgumentException("Ugyldig ThreatLogId", nameof(threatLogId));
        ActionType = !string.IsNullOrWhiteSpace(actionType) ? actionType : throw new ArgumentException("Handlingstype skal angives", nameof(actionType));
        IsSuccessful = isSuccessful;
        ExecutedAt = DateTime.UtcNow;
    }
}