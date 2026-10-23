using System.Security.Claims;
using Application.Interfaces;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers;

[ApiController]
[Authorize]
[Route("api/threatlogs")]
public sealed class ThreatLogsController : ControllerBase
{
    private readonly SecurityDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public ThreatLogsController(SecurityDbContext db, ICurrentTenantService tenant) => (_db, _tenant) = (db, tenant);

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var tenantClaim = User.FindFirstValue("tenant_id");
        if (!Guid.TryParse(tenantClaim, out var tenantId) || tenantId == Guid.Empty || _tenant.TenantId != tenantId)
            return Forbid();

        // The validated JWT tenant claim is authoritative. Apply it explicitly so this
        // result cannot depend on a query filter captured before tenant resolution.
        var logs = await _db.ThreatLogs.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.LastSeenAt)
            .Select(x => new { x.Id, x.SourceIp, x.RequestPath, x.AttackType, Severity = (int)x.Severity, x.HitCount, x.FirstSeenAt, x.LastSeenAt, x.CountryCode })
            .ToListAsync(cancellationToken);
        return Ok(logs);
    }

    [HttpGet("audit")]
    public async Task<IActionResult> Audit([FromQuery] DateTime? since = null, CancellationToken cancellationToken = default)
    {
        if (!_tenant.TenantId.HasValue) return Forbid();
        var tenantId = _tenant.TenantId.Value;
        var entries = await _db.SecurityAuditEvents.AsNoTracking()
            .Where(x => x.TenantId == tenantId && (!since.HasValue || x.OccurredAt >= since.Value))
            .OrderByDescending(x => x.OccurredAt)
            .Select(x => new { x.Id, x.EventType, x.Category, x.SourceIp, x.RequestPath, Severity = (int)x.Severity, x.OccurredAt, x.Details })
            .ToListAsync(cancellationToken);
        return Ok(entries);
    }

    [HttpGet("timeline")]
    public async Task<IActionResult> Timeline([FromQuery] string type = "all", [FromQuery] DateTime? since = null, CancellationToken cancellationToken = default)
    {
        if (!_tenant.TenantId.HasValue) return Forbid();
        var tenantId = _tenant.TenantId.Value;
        var result = new List<TimelineItem>();

        if (type is "all" or "threat")
        {
            var threats = await _db.ThreatLogs.AsNoTracking()
                .Where(x => x.TenantId == tenantId && (!since.HasValue || x.LastSeenAt >= since.Value))
                .Select(x => new TimelineItem(
                    x.Id,
                    "ThreatLog",
                    x.LastSeenAt,
                    x.AttackType,
                    x.SourceIp,
                    x.RequestPath,
                    (int)x.Severity,
                    x.HitCount,
                    null))
                .ToListAsync(cancellationToken);
            result.AddRange(threats);
        }

        if (type is "all" or "audit")
        {
            var auditEvents = await _db.SecurityAuditEvents.AsNoTracking()
                .Where(x => x.TenantId == tenantId && (!since.HasValue || x.OccurredAt >= since.Value))
                .Select(x => new TimelineItem(
                    x.Id,
                    "SecurityAuditEvent",
                    x.OccurredAt,
                    x.EventType,
                    x.SourceIp,
                    x.RequestPath,
                    (int)x.Severity,
                    null,
                    x.Details))
                .ToListAsync(cancellationToken);
            result.AddRange(auditEvents);
        }

        return Ok(result.OrderByDescending(x => x.OccurredAt));
    }

    public sealed record TimelineItem(Guid Id, string RecordType, DateTime OccurredAt, string EventType, string? SourceIp, string RequestPath, int Severity, int? HitCount, string? Details);
}
