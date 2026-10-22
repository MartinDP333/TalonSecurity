using Domain.Enums;
using System.Security.Claims;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers;

[ApiController]
[Authorize]
[Route("api/memberships")]
public sealed class MembershipsController : ControllerBase
{
    private readonly SecurityDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public MembershipsController(SecurityDbContext db, ICurrentTenantService tenant) => (_db, _tenant) = (db, tenant);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("PlatformAdmin")) return Forbid();
        var items = await _db.TenantMemberships.AsNoTracking()
            .Select(x => new { x.UserAccount.UserName, x.Role, x.IsActive, x.TenantId }).ToListAsync(cancellationToken);
        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> Invite(InviteRequest request, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("PlatformAdmin")) return Forbid();
        if (!_tenant.TenantId.HasValue) return Forbid();

        var user = await _db.UserAccounts.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.UserName == request.UserName, cancellationToken);
        if (user is null) return NotFound("Create the user account through the platform-admin endpoint first.");
        if (await _db.TenantMemberships.IgnoreQueryFilters().AnyAsync(x => x.TenantId == _tenant.TenantId.Value && x.UserAccountId == user.Id, cancellationToken))
            return Conflict("User is already a member of this tenant.");

        if (!Enum.TryParse<TenantRole>(request.Role, true, out var role)) return BadRequest(new { error = "Invalid tenant role.", allowedRoles = Enum.GetNames<TenantRole>() });
        _db.TenantMemberships.Add(new TenantMembership(_tenant.TenantId.Value, user.Id, role.ToString()));
        await _db.SaveChangesAsync(cancellationToken);
        return Created("api/memberships", new { userName = user.UserName, tenantId = _tenant.TenantId, request.Role });
    }

    [HttpDelete("{userName}")]
    public async Task<IActionResult> Remove(string userName, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("PlatformAdmin")) return Forbid();
        if (!_tenant.TenantId.HasValue) return Forbid();
        var membership = await _db.TenantMemberships.IgnoreQueryFilters().Include(x => x.UserAccount).SingleOrDefaultAsync(x => x.TenantId == _tenant.TenantId.Value && x.UserAccount.UserName == userName, cancellationToken);
        if (membership is null) return NotFound();
        membership.Deactivate();
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{userName}/activate")]
    public async Task<IActionResult> Activate(string userName, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("PlatformAdmin")) return Forbid();
        if (!_tenant.TenantId.HasValue) return Forbid();
        var membership = await _db.TenantMemberships.IgnoreQueryFilters().Include(x => x.UserAccount)
            .SingleOrDefaultAsync(x => x.TenantId == _tenant.TenantId.Value && x.UserAccount.UserName == userName, cancellationToken);
        if (membership is null) return NotFound();
        membership.Activate();
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("roles")]
    public IActionResult Roles() => Ok(Enum.GetNames<TenantRole>());

    public sealed record InviteRequest(string UserName, string Role = "Viewer");
}
