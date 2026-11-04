using System.Security.Claims;
using Application.Interfaces;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Authorize(Roles = "PlatformAdmin")]
[Route("api/platform-admin")]
public sealed class PlatformAdminController : ControllerBase
{
    private readonly IPlatformAdminService _platformAdmin;
    private readonly ICurrentTenantService _currentTenant;

    public PlatformAdminController(IPlatformAdminService platformAdmin, ICurrentTenantService currentTenant)
        => (_platformAdmin, _currentTenant) = (platformAdmin, currentTenant);

    [HttpGet("users")]
    public async Task<IActionResult> Users(CancellationToken ct)
    {
        var users = await _platformAdmin.GetUsersAsync(ct);
        return Ok(users);
    }

    [HttpGet("tenants")]
    public async Task<IActionResult> Tenants(CancellationToken ct)
    {
        var tenants = await _platformAdmin.GetTenantsAsync(ct);
        return Ok(tenants);
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser(CreateUserRequest request, CancellationToken ct)
    {
        var user = await _platformAdmin.CreateUserAsync(request.UserName, request.Password, request.IsPlatformAdmin, ct);
        if (user is null)
            return Conflict("Username already exists.");

        return Ok(new { user.Id, user.UserName });
    }

    [HttpPost("tenants")]
    public async Task<IActionResult> CreateTenant(CreateTenantRequest request, CancellationToken ct)
    {
        var tenant = await _platformAdmin.CreateTenantAsync(request.Name, request.OwnerUserName, ct);
        if (tenant is null)
            return BadRequest("Select an existing owner account and choose a unique tenant name.");

        return Ok(new { tenant.Id, tenant.Name });
    }

    [HttpPost("memberships")]
    public async Task<IActionResult> AddMembership(AddMembershipRequest request, CancellationToken ct)
    {
        using var targetTenant = _currentTenant.UseTenant(request.TenantId);
        var membership = await _platformAdmin.AddMembershipAsync(request.TenantId, request.UserName, request.Role, ct);
        if (membership is null)
        {
            if (!Enum.TryParse<TenantRole>(request.Role, true, out _))
                return BadRequest(new { error = "Invalid role.", allowedRoles = Enum.GetNames<TenantRole>() });

            return NotFound("User account or tenant was not found, or the membership already exists.");
        }

        return Ok(new { request.TenantId, UserName = membership.UserName, Role = membership.Role });
    }

    [HttpGet("memberships")]
    public async Task<IActionResult> Memberships(CancellationToken ct)
    {
        var memberships = await _platformAdmin.GetMembershipsAsync(ct);
        return Ok(memberships);
    }

    [HttpPut("memberships")]
    public async Task<IActionResult> UpdateMembership(UpdateMembershipRequest request, CancellationToken ct)
    {
        using var targetTenant = _currentTenant.UseTenant(request.TenantId);
        var membership = await _platformAdmin.UpdateMembershipAsync(request.TenantId, request.UserName, request.Role, ct);
        if (membership is null)
        {
            if (!Enum.TryParse<TenantRole>(request.Role, true, out _))
                return BadRequest(new { error = "Invalid role.", allowedRoles = Enum.GetNames<TenantRole>() });

            return NotFound("User account or membership was not found.");
        }

        return Ok(new { request.TenantId, UserName = membership.UserName, Role = membership.Role });
    }

    public sealed record CreateUserRequest(string UserName, string Password, bool IsPlatformAdmin = false);
    public sealed record CreateTenantRequest(string Name, string OwnerUserName);
    public sealed record AddMembershipRequest(Guid TenantId, string UserName, string Role);
    public sealed record UpdateMembershipRequest(Guid TenantId, string UserName, string Role);
}
