using System.Security.Claims;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Authorize]
[Route("api/tenants")]
public sealed class TenantsController : ControllerBase
{
    private readonly ICurrentTenantService _tenant;
    private readonly ITenantCatalogService _tenantCatalog;

    public TenantsController(ICurrentTenantService tenant, ITenantCatalogService tenantCatalog)
        => (_tenant, _tenantCatalog) = (tenant, tenantCatalog);

    [HttpGet]
    public async Task<IActionResult> GetTenants(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var tenants = await _tenantCatalog.GetUserTenantsAsync(userId, cancellationToken);
        return Ok(tenants);
    }

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent(CancellationToken cancellationToken)
    {
        if (!_tenant.TenantId.HasValue) return Forbid();

        var tenant = await _tenantCatalog.GetCurrentTenantAsync(_tenant.TenantId.Value, cancellationToken);
        return tenant is null ? NotFound() : Ok(tenant);
    }
}
