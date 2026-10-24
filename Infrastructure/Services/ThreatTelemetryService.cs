using Application.Interfaces;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class ThreatTelemetryService : IThreatTelemetryService
{
    private readonly SecurityDbContext _db;

    public ThreatTelemetryService(SecurityDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ThreatLogEntry>> GetThreatLogsAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.ThreatLogs.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.LastSeenAt)
            .Select(x => new ThreatLogEntry(
                x.Id,
                x.SourceIp,
                x.RequestPath,
                x.AttackType,
                (int)x.Severity,
                x.HitCount,
                x.FirstSeenAt,
                x.LastSeenAt,
                x.CountryCode))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AuditEntry>> GetAuditEntriesAsync(Guid tenantId, DateTime? since = null, CancellationToken ct = default)
        => await _db.SecurityAuditEvents.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenantId && (!since.HasValue || x.OccurredAt >= since.Value))
            .OrderByDescending(x => x.OccurredAt)
            .Select(x => new AuditEntry(
                x.Id,
                x.EventType,
                x.Category,
                x.SourceIp,
                x.RequestPath,
                (int)x.Severity,
                x.OccurredAt,
                x.Details))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TimelineEntry>> GetTimelineAsync(Guid tenantId, string type = "all", DateTime? since = null, CancellationToken ct = default)
    {
        var result = new List<TimelineEntry>();

        if (type is "all" or "threat")
        {
            var threats = await _db.ThreatLogs.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.TenantId == tenantId && (!since.HasValue || x.LastSeenAt >= since.Value))
                .Select(x => new TimelineEntry(
                    x.Id,
                    "ThreatLog",
                    x.LastSeenAt,
                    x.AttackType,
                    x.SourceIp,
                    x.RequestPath,
                    (int)x.Severity,
                    x.HitCount,
                    null))
                .ToListAsync(ct);

            result.AddRange(threats);
        }

        if (type is "all" or "audit")
        {
            var auditEvents = await _db.SecurityAuditEvents.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.TenantId == tenantId && (!since.HasValue || x.OccurredAt >= since.Value))
                .Select(x => new TimelineEntry(
                    x.Id,
                    "SecurityAuditEvent",
                    x.OccurredAt,
                    x.EventType,
                    x.SourceIp,
                    x.RequestPath,
                    (int)x.Severity,
                    null,
                    x.Details))
                .ToListAsync(ct);

            result.AddRange(auditEvents);
        }

        return result.OrderByDescending(x => x.OccurredAt).ToList();
    }
}
