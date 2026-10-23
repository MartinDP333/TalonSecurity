using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Authorize]
[Route("api/protected-sites")]
public sealed class ProtectedSitesController : ControllerBase
{
    private readonly ICurrentTenantService _tenant;
    private readonly IProtectedSiteService _protectedSites;

    public ProtectedSitesController(ICurrentTenantService tenant, IProtectedSiteService protectedSites)
        => (_tenant, _protectedSites) = (tenant, protectedSites);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (!_tenant.TenantId.HasValue) return Forbid();
        if (!User.IsInRole("Admin") && !User.IsInRole("PlatformAdmin")) return Forbid();

        var sites = await _protectedSites.ListForTenantAsync(_tenant.TenantId.Value, cancellationToken);
        return Ok(sites.Select(x => new { x.Id, x.TenantId, x.Hostname, x.OriginUrl, x.IsEnabled }));
    }

    [HttpPost]
    public async Task<IActionResult> Add(AddSiteRequest request, CancellationToken cancellationToken)
    {
        if (!_tenant.TenantId.HasValue) return Forbid();
        if (!User.IsInRole("Admin") && !User.IsInRole("PlatformAdmin")) return Forbid();

        var normalizedHostname = request.Hostname.Trim();
        if (await _protectedSites.IsHostnameRegisteredAsync(normalizedHostname, cancellationToken))
            return Conflict("Hostname is already registered.");

        var site = await _protectedSites.AddAsync(_tenant.TenantId.Value, normalizedHostname, request.OriginUrl, cancellationToken);
        return Created("api/protected-sites", new { site.Id, site.Hostname, site.OriginUrl, site.IsEnabled });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken cancellationToken)
    {
        if (!_tenant.TenantId.HasValue) return Forbid();
        if (!User.IsInRole("Admin") && !User.IsInRole("PlatformAdmin")) return Forbid();

        var site = await _protectedSites.GetByIdAsync(id, cancellationToken);
        if (site is null) return NotFound();

        await _protectedSites.DisableAsync(id, cancellationToken);
        return NoContent();
    }

    public sealed record AddSiteRequest(string Hostname, string OriginUrl);
}
