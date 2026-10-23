using Domain.Entities;

namespace Application.Interfaces;

public interface IPlatformAdminService
{
    Task<IReadOnlyList<UserAccountListItem>> GetUsersAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TenantListItem>> GetTenantsAsync(CancellationToken ct = default);
    Task<UserAccountListItem?> CreateUserAsync(string userName, string password, bool isPlatformAdmin = false, CancellationToken ct = default);
    Task<TenantListItem?> CreateTenantAsync(string tenantName, string ownerUserName, CancellationToken ct = default);
    Task<IReadOnlyList<TenantMembershipListItem>> GetMembershipsAsync(CancellationToken ct = default);
    Task<TenantMembershipListItem?> AddMembershipAsync(Guid tenantId, string userName, string role, CancellationToken ct = default);
    Task<TenantMembershipListItem?> UpdateMembershipAsync(Guid tenantId, string userName, string role, CancellationToken ct = default);
}

public sealed record UserAccountListItem(Guid Id, string UserName, bool IsPlatformAdmin);
public sealed record TenantListItem(Guid Id, string Name);
public sealed record TenantMembershipListItem(Guid TenantId, string TenantName, string UserName, string Role, bool IsActive);
