using System.Security.Claims;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Authorize]
[Route("api/memberships")]
public sealed class MembershipsController : ControllerBase
{
    private readonly IMembershipService _memberships;
    private readonly ICurrentTenantService _tenant;

    public MembershipsController(IMembershipService memberships, ICurrentTenantService tenant)
        => (_memberships, _tenant) = (memberships, tenant);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("PlatformAdmin")) return Forbid();
        if (!_tenant.TenantId.HasValue) return Forbid();
        var items = await _memberships.ListAsync(_tenant.TenantId.Value, cancellationToken);
        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> Invite(InviteRequest request, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("PlatformAdmin")) return Forbid();
        if (!_tenant.TenantId.HasValue) return Forbid();

        var membership = await _memberships.InviteAsync(_tenant.TenantId.Value, request.UserName, request.Role, cancellationToken);
        if (membership is null)
        {
            if (!Enum.TryParse<Domain.Enums.TenantRole>(request.Role, true, out _))
                return BadRequest(new { error = "Invalid tenant role.", allowedRoles = Enum.GetNames<Domain.Enums.TenantRole>() });

            return Conflict("User is already a member of this tenant or the user account does not exist.");
        }

        return Created("api/memberships", new { userName = membership.UserName, tenantId = membership.TenantId, role = membership.Role });
    }

    [HttpDelete("{userName}")]
    public async Task<IActionResult> Remove(string userName, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("PlatformAdmin")) return Forbid();
        if (!_tenant.TenantId.HasValue) return Forbid();

        var removed = await _memberships.RemoveAsync(_tenant.TenantId.Value, userName, cancellationToken);
        return removed ? NoContent() : NotFound();
    }

    [HttpPost("{userName}/activate")]
    public async Task<IActionResult> Activate(string userName, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("PlatformAdmin")) return Forbid();
        if (!_tenant.TenantId.HasValue) return Forbid();

        var activated = await _memberships.ActivateAsync(_tenant.TenantId.Value, userName, cancellationToken);
        return activated ? NoContent() : NotFound();
    }

    [HttpGet("roles")]
    public async Task<IActionResult> Roles(CancellationToken cancellationToken)
    {
        if (!_tenant.TenantId.HasValue) return Forbid();
        return Ok(await _memberships.GetRolesAsync());
    }

    public sealed record InviteRequest(string UserName, string Role = "Viewer");
}
