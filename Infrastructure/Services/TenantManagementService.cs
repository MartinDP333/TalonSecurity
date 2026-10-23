using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class TenantManagementService : IPlatformAdminService, ITenantCatalogService
{
    private readonly SecurityDbContext _db;

    public TenantManagementService(SecurityDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<UserAccountListItem>> GetUsersAsync(CancellationToken ct = default)
        => await _db.UserAccounts.IgnoreQueryFilters().AsNoTracking()
            .OrderBy(u => u.UserName)
            .Select(u => new UserAccountListItem(u.Id, u.UserName, u.IsPlatformAdmin))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TenantListItem>> GetTenantsAsync(CancellationToken ct = default)
        => await _db.Tenants.IgnoreQueryFilters().AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new TenantListItem(t.Id, t.Name))
            .ToListAsync(ct);

    public async Task<UserAccountListItem?> CreateUserAsync(string userName, string password, bool isPlatformAdmin = false, CancellationToken ct = default)
    {
        if (await _db.UserAccounts.IgnoreQueryFilters().AnyAsync(u => u.UserName == userName, ct))
            return null;

        var user = new UserAccount(userName, PasswordService.Hash(password), isPlatformAdmin);
        _db.UserAccounts.Add(user);
        await _db.SaveChangesAsync(ct);

        return new UserAccountListItem(user.Id, user.UserName, user.IsPlatformAdmin);
    }

    public async Task<TenantListItem?> CreateTenantAsync(string tenantName, string ownerUserName, CancellationToken ct = default)
    {
        var owner = await _db.UserAccounts.IgnoreQueryFilters().SingleOrDefaultAsync(u => u.UserName == ownerUserName, ct);
        if (owner is null)
            return null;

        if (await _db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Name == tenantName, ct))
            return null;

        var tenant = new Tenant(tenantName);
        _db.Tenants.Add(tenant);
        _db.TenantMemberships.Add(new TenantMembership(tenant.Id, owner.Id, TenantRole.Admin.ToString()));
        await _db.SaveChangesAsync(ct);

        return new TenantListItem(tenant.Id, tenant.Name);
    }

    public async Task<IReadOnlyList<TenantMembershipListItem>> GetMembershipsAsync(CancellationToken ct = default)
        => await _db.TenantMemberships.IgnoreQueryFilters().AsNoTracking()
            .OrderBy(m => m.Tenant.Name)
            .ThenBy(m => m.UserAccount.UserName)
            .Select(m => new TenantMembershipListItem(m.TenantId, m.Tenant.Name, m.UserAccount.UserName, m.Role, m.IsActive))
            .ToListAsync(ct);

    public async Task<TenantMembershipListItem?> AddMembershipAsync(Guid tenantId, string userName, string role, CancellationToken ct = default)
    {
        if (!Enum.TryParse<TenantRole>(role, true, out var parsedRole))
            return null;

        var user = await _db.UserAccounts.IgnoreQueryFilters().SingleOrDefaultAsync(u => u.UserName == userName, ct);
        if (user is null)
            return null;

        var tenantExists = await _db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == tenantId, ct);
        if (!tenantExists)
            return null;

        var existing = await _db.TenantMemberships.IgnoreQueryFilters()
            .SingleOrDefaultAsync(m => m.TenantId == tenantId && m.UserAccountId == user.Id, ct);
        if (existing is not null)
            return null;

        var tenantName = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.Name)
            .SingleAsync(ct);

        var membership = new TenantMembership(tenantId, user.Id, parsedRole.ToString());
        _db.TenantMemberships.Add(membership);
        await _db.SaveChangesAsync(ct);

        return new TenantMembershipListItem(membership.TenantId, tenantName, user.UserName, membership.Role, membership.IsActive);
    }

    public async Task<TenantMembershipListItem?> UpdateMembershipAsync(Guid tenantId, string userName, string role, CancellationToken ct = default)
    {
        if (!Enum.TryParse<TenantRole>(role, true, out var parsedRole))
            return null;

        var user = await _db.UserAccounts.IgnoreQueryFilters().SingleOrDefaultAsync(u => u.UserName == userName, ct);
        if (user is null)
            return null;

        var membership = await _db.TenantMemberships.IgnoreQueryFilters()
            .SingleOrDefaultAsync(m => m.TenantId == tenantId && m.UserAccountId == user.Id, ct);
        if (membership is null)
            return null;

        var tenantName = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.Name)
            .SingleAsync(ct);

        membership.SetRole(parsedRole.ToString());
        await _db.SaveChangesAsync(ct);

        return new TenantMembershipListItem(membership.TenantId, tenantName, user.UserName, membership.Role, membership.IsActive);
    }

    public async Task<IReadOnlyList<UserTenantListItem>> GetUserTenantsAsync(string userName, CancellationToken ct = default)
        => await _db.TenantMemberships.IgnoreQueryFilters()
            .Where(m => m.UserAccount.UserName == userName && m.IsActive)
            .Select(m => new UserTenantListItem(m.TenantId, m.Tenant.Name, m.Role))
            .ToListAsync(ct);

    public async Task<TenantDetail?> GetCurrentTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.Tenants.IgnoreQueryFilters()
            .Where(t => t.Id == tenantId)
            .Select(t => new TenantDetail(t.Id, t.Name, t.CreatedAt))
            .SingleOrDefaultAsync(ct);
}
