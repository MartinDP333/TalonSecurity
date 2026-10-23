using Application.Interfaces;
using API.Services;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace API.Controllers;

[ApiController]
[Route("test-apps")]
public sealed class TestApplicationsController : ControllerBase
{
    private readonly ICurrentTenantService _tenant;
    private readonly SecurityAuditLogger _audit;

    public TestApplicationsController(ICurrentTenantService tenant, SecurityAuditLogger audit) => (_tenant, _audit) = (tenant, audit);

    [HttpGet("webshop")]
    public async Task<IActionResult> WebShop(CancellationToken ct)
    {
        await _audit.WriteAsync("TestApplicationRequest", "SensitiveAccess", "Synthetic WebShop test application request.", StatusCodes.Status200OK, User.FindFirstValue("sub"), _tenant.TenantId, ct);
        return Ok(new
        {
            application = "WebShop",
            tenantId = _tenant.TenantId,
            host = Request.Host.Host,
            message = "Synthetic WebShop tenant target"
        });
    }

    [HttpGet("bank")]
    public async Task<IActionResult> Bank(CancellationToken ct)
    {
        await _audit.WriteAsync("TestApplicationRequest", "SensitiveAccess", "Synthetic Bank test application request.", StatusCodes.Status200OK, User.FindFirstValue("sub"), _tenant.TenantId, ct);
        return Ok(new
        {
            application = "Bank",
            tenantId = _tenant.TenantId,
            host = Request.Host.Host,
            message = "Synthetic Bank tenant target"
        });
    }
}
