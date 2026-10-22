using System.Security.Claims;
using Application.Interfaces;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers;

[ApiController]
[Authorize]
[Route("api/tenants")]
public sealed class TenantsController : ControllerBase
{
    private readonly SecurityDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public TenantsController(SecurityDbContext db, ICurrentTenantService tenant) => (_db, _tenant) = (db, tenant);

    [HttpGet]
    public async Task<IActionResult> GetTenants(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var tenants = await _db.TenantMemberships.IgnoreQueryFilters()
            .Where(m => m.UserAccount.UserName == userId && m.IsActive)
            .Select(m => new { m.TenantId, TenantName = m.Tenant.Name, m.Role })
            .ToListAsync(cancellationToken);
        return Ok(tenants);
    }

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent(CancellationToken cancellationToken)
    {
        if (!_tenant.TenantId.HasValue) return Forbid();
        var tenant = await _db.Tenants.IgnoreQueryFilters().Where(t => t.Id == _tenant.TenantId.Value)
            .Select(t => new { t.Id, t.Name, t.CreatedAt }).SingleOrDefaultAsync(cancellationToken);
        return tenant is null ? NotFound() : Ok(tenant);
    }
}
