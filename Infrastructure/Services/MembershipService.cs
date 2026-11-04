using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class MembershipService : IMembershipService
{
    private readonly SecurityDbContext _db;

    public MembershipService(SecurityDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<MembershipOverviewItem>> ListAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.TenantMemberships.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .Select(x => new MembershipOverviewItem(x.UserAccount.UserName, x.Role, x.IsActive, x.TenantId))
            .ToListAsync(ct);

    public async Task<MembershipOverviewItem?> InviteAsync(Guid tenantId, string userName, string role, CancellationToken ct = default)
    {
        if (!Enum.TryParse<TenantRole>(role, true, out var parsedRole))
            return null;

        var user = await _db.UserAccounts.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.UserName == userName, ct);
        if (user is null)
            return null;

        if (await _db.TenantMemberships.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId && x.UserAccountId == user.Id, ct))
            return null;

        _db.TenantMemberships.Add(new TenantMembership(tenantId, user.Id, parsedRole.ToString()));
        await _db.SaveChangesAsync(ct);

        return new MembershipOverviewItem(user.UserName, parsedRole.ToString(), true, tenantId);
    }

    public async Task<bool> RemoveAsync(Guid tenantId, string userName, CancellationToken ct = default)
    {
        var membership = await _db.TenantMemberships.IgnoreQueryFilters()
            .Include(x => x.UserAccount)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.UserAccount.UserName == userName, ct);

        if (membership is null)
            return false;

        membership.Deactivate();
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ActivateAsync(Guid tenantId, string userName, CancellationToken ct = default)
    {
        var membership = await _db.TenantMemberships.IgnoreQueryFilters()
            .Include(x => x.UserAccount)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.UserAccount.UserName == userName, ct);

        if (membership is null)
            return false;

        membership.Activate();
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public Task<IReadOnlyList<string>> GetRolesAsync()
        => Task.FromResult<IReadOnlyList<string>>(Enum.GetNames<TenantRole>());
}
