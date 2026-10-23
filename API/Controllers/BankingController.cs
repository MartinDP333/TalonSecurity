using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class BankingController : ControllerBase
{
    private readonly ICurrentTenantService _tenantService;

    public BankingController(ICurrentTenantService tenantService) => _tenantService = tenantService;

    [HttpGet("dashboard")]
    public IActionResult Dashboard()
    {
        var sourceIp = GetClientIp();

        return Ok(new
        {
            application = "Banking",
            tenantId = _tenantService.TenantId,
            message = "Synthetic banking tenant target",
            accountHolder = "Jane Doe",
            accountNumber = "DK-18475020",
            availableBalance = 24589.32m,
            sourceIp,
            timestamp = DateTime.UtcNow
        });
    }

    [HttpPost("transfer")]
    public IActionResult Transfer([FromBody] TransferRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FromAccount) || string.IsNullOrWhiteSpace(request.ToAccount))
            return BadRequest(new { error = "Both account numbers are required." });

        if (request.Amount <= 0)
            return BadRequest(new { error = "Transfer amount must be greater than zero." });

        var sourceIp = GetClientIp();

        return Ok(new
        {
            application = "Banking",
            tenantId = _tenantService.TenantId,
            message = $"Transfer of {request.Amount:C} from {request.FromAccount} to {request.ToAccount} queued successfully.",
            sourceIp,
            status = "Queued",
            timestamp = DateTime.UtcNow
        });
    }

    private string GetClientIp()
    {
        var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        return !string.IsNullOrWhiteSpace(forwardedFor)
            ? forwardedFor
            : HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
    }

    public sealed record TransferRequest(string FromAccount, string ToAccount, decimal Amount, string? Description);
}
