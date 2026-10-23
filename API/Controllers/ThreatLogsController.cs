using System.Security.Claims;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Authorize]
[Route("api/threatlogs")]
public sealed class ThreatLogsController : ControllerBase
{
    private readonly ICurrentTenantService _tenant;
    private readonly IThreatTelemetryService _telemetry;

    public ThreatLogsController(ICurrentTenantService tenant, IThreatTelemetryService telemetry)
        => (_tenant, _telemetry) = (tenant, telemetry);

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var tenantClaim = User.FindFirstValue("tenant_id");
        if (!Guid.TryParse(tenantClaim, out var tenantId) || tenantId == Guid.Empty || _tenant.TenantId != tenantId)
            return Forbid();

        var logs = await _telemetry.GetThreatLogsAsync(tenantId, cancellationToken);
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
}
