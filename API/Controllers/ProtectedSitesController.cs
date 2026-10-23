using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers;

[ApiController]
[Authorize]
[Route("api/protected-sites")]
public sealed class ProtectedSitesController : ControllerBase
{
    private readonly SecurityDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public ProtectedSitesController(SecurityDbContext db, ICurrentTenantService tenant) => (_db, _tenant) = (db, tenant);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (!_tenant.TenantId.HasValue) return Forbid();
        if (!User.IsInRole("Admin") && !User.IsInRole("PlatformAdmin")) return Forbid();
        var tenantId = _tenant.TenantId.Value;
        var sites = await _db.ProtectedSites.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .Select(x => new { x.Id, x.TenantId, x.Hostname, x.OriginUrl, x.IsEnabled })
            .ToListAsync(cancellationToken);
        return Ok(sites);
    }

    [HttpPost]
    public async Task<IActionResult> Add(AddSiteRequest request, CancellationToken cancellationToken)
    {
        if (!_tenant.TenantId.HasValue) return Forbid();
        if (!User.IsInRole("Admin") && !User.IsInRole("PlatformAdmin")) return Forbid();
        if (await _db.ProtectedSites.IgnoreQueryFilters().AnyAsync(x => x.Hostname == request.Hostname.Trim().ToLower(), cancellationToken)) return Conflict("Hostname is already registered.");
        var site = new ProtectedSite(_tenant.TenantId.Value, request.Hostname, request.OriginUrl);
        _db.ProtectedSites.Add(site);
        await _db.SaveChangesAsync(cancellationToken);
        return Created("api/protected-sites", new { site.Id, site.Hostname, site.OriginUrl, site.IsEnabled });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken cancellationToken)
    {
        if (!_tenant.TenantId.HasValue) return Forbid();
        if (!User.IsInRole("Admin") && !User.IsInRole("PlatformAdmin")) return Forbid();
        var site = await _db.ProtectedSites.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (site is null) return NotFound();
        site.Disable();
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    public sealed record AddSiteRequest(string Hostname, string OriginUrl);
}
