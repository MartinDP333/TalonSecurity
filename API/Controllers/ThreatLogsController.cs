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
        if (!_tenant.TenantId.HasValue) return Forbid();
        var logs = await _db.ThreatLogs.AsNoTracking().OrderByDescending(x => x.LastSeenAt)
            .Select(x => new { x.Id, x.SourceIp, x.RequestPath, x.AttackType, x.Severity, x.HitCount, x.FirstSeenAt, x.LastSeenAt, x.CountryCode })
            .ToListAsync(cancellationToken);
        return Ok(logs);
    }

    [HttpGet("timeline")]
    public async Task<IActionResult> Timeline([FromQuery] string type = "all", [FromQuery] DateTime? since = null, CancellationToken cancellationToken = default)
    {
        if (!_tenant.TenantId.HasValue) return Forbid();
        var items = new List<TimelineItem>();

        if (type is "all" or "threat")
        {
            var threats = await _db.ThreatLogs.AsNoTracking()
                .Where(x => !since.HasValue || x.LastSeenAt >= since.Value)
                .Select(x => new { x.Id, x.SourceIp, x.RequestPath, x.AttackType, x.Severity, x.HitCount, x.FirstSeenAt, x.LastSeenAt })
                .ToListAsync(cancellationToken);
            items.AddRange(threats.Select(x => new TimelineItem(x.Id, "ThreatLog", x.LastSeenAt, x.AttackType, x.SourceIp, x.RequestPath, (int)x.Severity, x.HitCount, null)));
        }

        if (type is "all" or "audit")
        {
            var audit = await _db.SecurityAuditEvents.AsNoTracking()
                .Where(x => !since.HasValue || x.OccurredAt >= since.Value)
                .Select(x => new { x.Id, x.EventType, x.Category, x.SourceIp, x.RequestPath, x.Severity, x.OccurredAt, x.Details })
                .ToListAsync(cancellationToken);
            items.AddRange(audit.Select(x => new TimelineItem(x.Id, "SecurityAuditEvent", x.OccurredAt, x.EventType, x.SourceIp, x.RequestPath, x.Severity, null, x.Details)));
        }

        return Ok(items.OrderByDescending(x => x.OccurredAt));
    }

    public sealed record TimelineItem(Guid Id, string RecordType, DateTime OccurredAt, string EventType, string? SourceIp, string RequestPath, int Severity, int? HitCount, string? Details);
}
