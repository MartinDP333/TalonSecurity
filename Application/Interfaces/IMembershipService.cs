namespace Application.Interfaces;

public interface IMembershipService
{
    Task<IReadOnlyList<MembershipOverviewItem>> ListAsync(CancellationToken ct = default);
    Task<MembershipOverviewItem?> InviteAsync(Guid tenantId, string userName, string role, CancellationToken ct = default);
    Task<bool> RemoveAsync(Guid tenantId, string userName, CancellationToken ct = default);
    Task<bool> ActivateAsync(Guid tenantId, string userName, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetRolesAsync();
}

public sealed record MembershipOverviewItem(string UserName, string Role, bool IsActive, Guid TenantId);
