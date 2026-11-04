namespace Application.Interfaces;

public interface IThreatTelemetryService
{
    Task<IReadOnlyList<ThreatLogEntry>> GetThreatLogsAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<AuditEntry>> GetAuditEntriesAsync(Guid tenantId, DateTime? since = null, CancellationToken ct = default);
    Task<IReadOnlyList<TimelineEntry>> GetTimelineAsync(Guid tenantId, string type = "all", DateTime? since = null, CancellationToken ct = default);
}

public sealed record ThreatLogEntry(Guid Id, string SourceIp, string RequestPath, string AttackType, int Severity, int HitCount, DateTime FirstSeenAt, DateTime LastSeenAt, string CountryCode);
public sealed record AuditEntry(Guid Id, string EventType, string Category, string? SourceIp, string CountryCode, string HttpMethod, string RequestPath, int? StatusCode, int Severity, DateTime OccurredAt, string? Details);
public sealed record TimelineEntry(Guid Id, string RecordType, DateTime OccurredAt, string EventType, string? SourceIp, string CountryCode, string HttpMethod, int? StatusCode, string RequestPath, int Severity, int? HitCount, string? Details);
