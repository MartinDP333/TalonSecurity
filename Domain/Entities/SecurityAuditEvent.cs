using Domain.Enums;
using System;

namespace Domain.Entities;

public class SecurityAuditEvent : BaseEntity
{
    public Guid? TenantId { get; private set; }
    public string? UserName { get; private set; }
    public string EventType { get; private set; }
    public string Category { get; private set; }
    public string HttpMethod { get; private set; }
    public string RequestPath { get; private set; }
    public string? SourceIp { get; private set; }
    public int? StatusCode { get; private set; }
    public string Details { get; private set; }
    public SeverityLevel Severity { get; private set; }
    public DateTime OccurredAt { get; private set; }

    private SecurityAuditEvent() { }

    public SecurityAuditEvent(Guid? tenantId, string? userName, string eventType, string category, string httpMethod, string requestPath, string? sourceIp, int? statusCode, string? details, SeverityLevel severity = SeverityLevel.Low)
    {
        TenantId = tenantId;
        UserName = Limit(userName, 200);
        EventType = Limit(eventType, 100) ?? "Unknown";
        Category = Limit(category, 100) ?? "General";
        HttpMethod = Limit(httpMethod, 20) ?? "";
        RequestPath = Limit(requestPath, 500) ?? "";
        SourceIp = Limit(sourceIp, 45);
        StatusCode = statusCode;
        Details = Limit(details, 2000) ?? string.Empty;
        Severity = severity;
        OccurredAt = DateTime.UtcNow;
    }

    private static string? Limit(string? value, int max) => string.IsNullOrEmpty(value) ? value : value[..Math.Min(value.Length, max)];
}
