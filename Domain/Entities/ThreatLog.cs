using Domain.Enums;
using System;

namespace Domain.Entities;

public class ThreatLog : BaseEntity
{
    public Guid TenantId { get; private set; }
    public Tenant Tenant { get; private set; } = null!;
    public string SourceIp { get; private set; }
    public string RequestPath { get; private set; }
    public string AttackType { get; private set; }
    public SeverityLevel Severity { get; private set; }
    public string RawPayload { get; private set; }
    public string CountryCode { get; private set; }
    public int HitCount { get; private set; }
    public DateTime FirstSeenAt { get; private set; }
    public DateTime LastSeenAt { get; private set; }

    // Navigation
    public Notification? Notification { get; private set; }
    public AutomatedResponseAction? ResponseAction { get; private set; }

    private ThreatLog() { }

    public ThreatLog(Guid tenantId, string sourceIp, string requestPath, string attackType, SeverityLevel severity, string rawPayload, string countryCode)
    {
        TenantId = tenantId != Guid.Empty ? tenantId : throw new ArgumentException("Ugyldig TenantId", nameof(tenantId));
        SourceIp = !string.IsNullOrWhiteSpace(sourceIp) ? sourceIp : throw new ArgumentException("SourceIp skal angives", nameof(sourceIp));
        RequestPath = requestPath ?? string.Empty;
        AttackType = attackType ?? string.Empty;
        Severity = severity;
        RawPayload = rawPayload ?? string.Empty;
        CountryCode = countryCode ?? string.Empty;

        HitCount = 1;
        FirstSeenAt = DateTime.UtcNow;
        LastSeenAt = DateTime.UtcNow;
    }

    public void RegisterNewHit()
    {
        HitCount++;
        LastSeenAt = DateTime.UtcNow;
    }
}