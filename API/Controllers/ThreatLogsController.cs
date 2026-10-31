using System.Security.Claims;
using Application.Interfaces;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Authorize]
[Route("api/threatlogs")]
public sealed class ThreatLogsController : ControllerBase
{
    private readonly ICurrentTenantService _tenant;
    private readonly ITenantCatalogService _tenantCatalog;
    private readonly IThreatTelemetryService _telemetry;

    public ThreatLogsController(ICurrentTenantService tenant, ITenantCatalogService tenantCatalog, IThreatTelemetryService telemetry)
        => (_tenant, _tenantCatalog, _telemetry) = (tenant, tenantCatalog, telemetry);

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (!_tenant.TenantId.HasValue || _tenant.TenantId.Value == Guid.Empty)
            return Forbid();

        var logs = await _telemetry.GetThreatLogsAsync(_tenant.TenantId.Value, cancellationToken);
        return Ok(logs);
    }

    [HttpGet("audit")]
    public async Task<IActionResult> Audit([FromQuery] DateTime? since = null, CancellationToken cancellationToken = default)
    {
        if (!_tenant.TenantId.HasValue) return Forbid();

        var entries = await _telemetry.GetAuditEntriesAsync(_tenant.TenantId.Value, since, cancellationToken);
        return Ok(entries);
    }

    [HttpGet("timeline")]
    public async Task<IActionResult> Timeline([FromQuery] string type = "all", [FromQuery] DateTime? since = null, CancellationToken cancellationToken = default)
    {
        if (!_tenant.TenantId.HasValue) return Forbid();

        var result = await _telemetry.GetTimelineAsync(_tenant.TenantId.Value, type, since, cancellationToken);
        return Ok(result);
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> Notifications([FromQuery] int take = 8, CancellationToken cancellationToken = default)
    {
        var userName = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(userName)) return Unauthorized();

        var connectedTenants = await _tenantCatalog.GetUserTenantsAsync(userName, cancellationToken);
        if (connectedTenants.Count == 0)
            return Ok(Array.Empty<TenantAlertNotification>());

        var notifications = new List<TenantAlertNotification>();

        foreach (var tenant in connectedTenants)
        {
            var threats = await _telemetry.GetThreatLogsAsync(tenant.TenantId, cancellationToken);
            notifications.AddRange(threats
                .Where(x => x.Severity >= (int)SeverityLevel.High)
                .Select(x => new TenantAlertNotification(
                    tenant.TenantId,
                    tenant.TenantName,
                    "ThreatLog",
                    x.AttackType,
                    x.Severity,
                    x.SourceIp,
                    x.RequestPath,
                    $"{x.AttackType} was seen {x.HitCount} times from {x.SourceIp}.",
                    x.LastSeenAt)));

            var audits = await _telemetry.GetAuditEntriesAsync(tenant.TenantId, null, cancellationToken);
            notifications.AddRange(audits
                .Where(x => x.Severity >= (int)SeverityLevel.High)
                .Select(x => new TenantAlertNotification(
                    tenant.TenantId,
                    tenant.TenantName,
                    "SecurityAuditEvent",
                    x.EventType,
                    x.Severity,
                    x.SourceIp,
                    x.RequestPath,
                    x.Details ?? x.EventType,
                    x.OccurredAt)));
        }

        var result = notifications
            .OrderByDescending(x => x.OccurredAt)
            .Take(Math.Max(1, take))
            .ToList();

        return Ok(result);
    }

    public sealed record TenantAlertNotification(
        Guid TenantId,
        string TenantName,
        string RecordType,
        string EventType,
        int Severity,
        string? SourceIp,
        string RequestPath,
        string Details,
        DateTime OccurredAt);
}
