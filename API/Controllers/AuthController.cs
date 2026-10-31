using System.Security.Claims;
using API.Services;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authentication;
    private readonly IPlatformAdminService _platformAdmin;
    private readonly IProtectedSiteService _protectedSites;
    private readonly SecurityAuditLogger _audit;

    public AuthController(IAuthenticationService authentication, IPlatformAdminService platformAdmin, IProtectedSiteService protectedSites, SecurityAuditLogger audit)
        => (_authentication, _platformAdmin, _protectedSites, _audit) = (authentication, platformAdmin, protectedSites, audit);

    [HttpPost("users")]
    [Authorize(Roles = "PlatformAdmin")]
    public async Task<IActionResult> CreateUser(CreateUserRequest request, CancellationToken ct)
    {
        var user = await _platformAdmin.CreateUserAsync(request.UserName, request.Password, request.IsPlatformAdmin, ct);
        if (user is null)
            return Conflict("Username already exists.");

        return CreatedAtAction(nameof(AvailableTenants), new { userName = user.UserName }, new { user.Id, user.UserName, user.IsPlatformAdmin });
    }

    [HttpPost("tenants")]
    [Authorize(Roles = "PlatformAdmin")]
    public async Task<IActionResult> CreateTenant(CreateTenantRequest request, CancellationToken ct)
    {
        var tenant = await _platformAdmin.CreateTenantAsync(request.TenantName, request.OwnerUserName, ct);
        if (tenant is null)
            return BadRequest("Owner user does not exist or tenant name is already in use.");

        return Created("/api/tenants", new { tenant.Id, tenant.Name, OwnerUserName = request.OwnerUserName, OwnerRole = "Admin" });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var user = await _authentication.ValidateUserAsync(request.UserName, request.Password, ct);
        if (user is null)
        {
            await _audit.WriteAsync("LoginFailure", "Authentication", "Invalid username or password.", StatusCodes.Status401Unauthorized, request.UserName, ct: ct);
            return Unauthorized();
        }

        var membership = await _authentication.GetActiveMembershipAsync(user.Id, request.TenantName, ct);
        if (membership is null)
        {
            await _audit.WriteAsync("LoginFailure", "Authentication", "No active membership for selected tenant.", StatusCodes.Status401Unauthorized, user.UserName, ct: ct);
            return Unauthorized("No active membership exists for this tenant.");
        }

        var protectedHostname = Request.Host.Host;
        var siteTenantId = await _protectedSites.ResolveTenantIdByHostnameAsync(protectedHostname);
        if (string.IsNullOrWhiteSpace(protectedHostname) || !siteTenantId.HasValue)
            return BadRequest("Login must target an enabled protected-site hostname.");

        await _audit.WriteAsync("LoginSuccess", "Authentication", $"Tenant: {membership.Tenant.Name}; protected site: {protectedHostname}", StatusCodes.Status200OK, user.UserName, membership.TenantId, ct);
        return Ok(await _authentication.CreateTokenAsync(user, membership, protectedHostname, siteTenantId.Value));
    }

    [HttpPost("switch-tenant")]
    [Authorize]
    public async Task<IActionResult> SwitchTenant(SwitchTenantRequest request, CancellationToken ct)
    {
        var userName = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(userName)) return Unauthorized();

        var user = await _authentication.GetUserByNameAsync(userName, ct);
        if (user is null)
        {
            var username = User.Identity?.Name ?? userName;
            await _audit.WriteAsync("AuthorizationFailure", "Authentication", "Unable to resolve current user while switching tenant.", StatusCodes.Status401Unauthorized, username, ct: ct);
            return Unauthorized();
        }

        var membership = await _authentication.GetActiveMembershipAsync(user.Id, request.TenantName, ct);
        if (membership is null)
            return Forbid();

        var protectedHostname = User.FindFirstValue("hostname");
        if (string.IsNullOrWhiteSpace(protectedHostname)
            || !string.Equals(protectedHostname, Request.Host.Host, StringComparison.OrdinalIgnoreCase))
            return Forbid();

        var siteTenantId = await _protectedSites.ResolveTenantIdByHostnameAsync(protectedHostname);
        if (!siteTenantId.HasValue)
            return Forbid();

        return Ok(await _authentication.CreateTokenAsync(user, membership, protectedHostname, siteTenantId.Value));
    }

    [HttpPost("available-tenants")]
    [AllowAnonymous]
    public async Task<IActionResult> AvailableTenants(CredentialsRequest request, CancellationToken ct)
    {
        var membershipSummaries = await _authentication.GetAvailableTenantsAsync(request.UserName, request.Password, ct);
        if (membershipSummaries.Count == 0)
            return Unauthorized();

        return Ok(membershipSummaries.Select(x => new { x.TenantId, TenantName = x.TenantName, x.Role, x.Hostname }).ToList());
    }

    public sealed record CreateUserRequest(string UserName, string Password, bool IsPlatformAdmin = false);
    public sealed record CreateTenantRequest(string TenantName, string OwnerUserName);
    public sealed record CredentialsRequest(string UserName, string Password);
    public sealed record LoginRequest(string UserName, string Password, string TenantName, string? Hostname = null);
    public sealed record SwitchTenantRequest(string TenantName, string? Hostname = null);
}
