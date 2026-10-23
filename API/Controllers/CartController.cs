using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CartController : ControllerBase
{
    private readonly ICurrentTenantService _tenantService;

    public CartController(ICurrentTenantService tenantService) => _tenantService = tenantService;

    [HttpPost("add")]
    public IActionResult AddToCart([FromBody] string item)
    {
        var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        var sourceIp = !string.IsNullOrWhiteSpace(forwardedFor)
            ? forwardedFor
            : HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

        return Ok(new
        {
            Message = $"Item '{item}' added",
            Ip = sourceIp,
            TenantId = _tenantService.TenantId
        });
    }
}
