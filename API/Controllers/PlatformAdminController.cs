using System.Security.Claims;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers;

[ApiController]
[Authorize(Roles = "PlatformAdmin")]
[Route("api/platform-admin")]
public sealed class PlatformAdminController : ControllerBase
{
    private readonly SecurityDbContext _db;
    public PlatformAdminController(SecurityDbContext db) => _db = db;

    [HttpGet("users")]
    public async Task<IActionResult> Users(CancellationToken ct)
    {
        var users = await _db.UserAccounts.IgnoreQueryFilters().AsNoTracking()
            .OrderBy(u => u.UserName)
            .Select(u => new { u.Id, u.UserName, u.IsPlatformAdmin })
            .ToListAsync(ct);
        return Ok(users);
    }

    [HttpGet("tenants")]
    public async Task<IActionResult> Tenants(CancellationToken ct)
    {
        var tenants = await _db.Tenants.IgnoreQueryFilters().AsNoTracking()
            .OrderBy(t => t.Name).Select(t => new { t.Id, t.Name }).ToListAsync(ct);
        return Ok(tenants);
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser(CreateUserRequest request, CancellationToken ct)
    {
        if (await _db.UserAccounts.IgnoreQueryFilters().AnyAsync(u => u.UserName == request.UserName, ct))
            return Conflict("Username already exists.");
        var user = new UserAccount(request.UserName, API.Services.PasswordService.Hash(request.Password));
        _db.UserAccounts.Add(user);
        await _db.SaveChangesAsync(ct);
        return Ok(new { user.Id, user.UserName });
    }

    [HttpPost("tenants")]
    public async Task<IActionResult> CreateTenant(CreateTenantRequest request, CancellationToken ct)
    {
        var owner = await _db.UserAccounts.IgnoreQueryFilters().SingleOrDefaultAsync(u => u.UserName == request.OwnerUserName, ct);
        if (owner is null) return BadRequest("Select an existing owner account.");
        if (await _db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Name == request.Name, ct)) return Conflict("Tenant name already exists.");
        var tenant = new Tenant(request.Name);
        _db.Tenants.Add(tenant);
        _db.TenantMemberships.Add(new TenantMembership(tenant.Id, owner.Id, TenantRole.Admin.ToString()));
        await _db.SaveChangesAsync(ct);
        return Ok(new { tenant.Id, tenant.Name, Owner = owner.UserName });
    }

    [HttpPost("memberships")]
    public async Task<IActionResult> AddMembership(AddMembershipRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<TenantRole>(request.Role, true, out var role))
            return BadRequest(new { error = "Invalid role.", allowedRoles = Enum.GetNames<TenantRole>() });
        var user = await _db.UserAccounts.IgnoreQueryFilters().SingleOrDefaultAsync(u => u.UserName == request.UserName, ct);
        if (user is null) return NotFound("User account was not found.");
        var tenantExists = await _db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == request.TenantId, ct);
        if (!tenantExists) return NotFound("Tenant was not found.");

        var existing = await _db.TenantMemberships.IgnoreQueryFilters()
            .SingleOrDefaultAsync(m => m.TenantId == request.TenantId && m.UserAccountId == user.Id, ct);
        if (existing is not null) return Conflict("User already has a membership in this tenant.");

        _db.TenantMemberships.Add(new TenantMembership(request.TenantId, user.Id, role.ToString()));
        await _db.SaveChangesAsync(ct);
        return Ok(new { request.TenantId, UserName = user.UserName, Role = role.ToString() });
    }

    [HttpGet("memberships")]
    public async Task<IActionResult> Memberships(CancellationToken ct)
    {
        var memberships = await _db.TenantMemberships.IgnoreQueryFilters().AsNoTracking()
            .OrderBy(m => m.Tenant.Name).ThenBy(m => m.UserAccount.UserName)
            .Select(m => new { m.TenantId, TenantName = m.Tenant.Name, UserName = m.UserAccount.UserName, m.Role, m.IsActive })
            .ToListAsync(ct);
        return Ok(memberships);
    }

    [HttpPut("memberships")]
    public async Task<IActionResult> UpdateMembership(UpdateMembershipRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<TenantRole>(request.Role, true, out var role))
            return BadRequest(new { error = "Invalid role.", allowedRoles = Enum.GetNames<TenantRole>() });
        var user = await _db.UserAccounts.IgnoreQueryFilters().SingleOrDefaultAsync(u => u.UserName == request.UserName, ct);
        if (user is null) return NotFound("User account was not found.");
        var membership = await _db.TenantMemberships.IgnoreQueryFilters()
            .SingleOrDefaultAsync(m => m.TenantId == request.TenantId && m.UserAccountId == user.Id, ct);
        if (membership is null) return NotFound("Membership was not found.");
        membership.SetRole(role.ToString());
        await _db.SaveChangesAsync(ct);
        return Ok(new { request.TenantId, UserName = user.UserName, Role = role.ToString() });
    }

    public sealed record CreateUserRequest(string UserName, string Password);
    public sealed record CreateTenantRequest(string Name, string OwnerUserName);
    public sealed record AddMembershipRequest(Guid TenantId, string UserName, string Role);
    public sealed record UpdateMembershipRequest(Guid TenantId, string UserName, string Role);
}
