namespace Application.Interfaces;

public interface ITenantCatalogService
{
    Task<IReadOnlyList<UserTenantListItem>> GetUserTenantsAsync(string userName, CancellationToken ct = default);
    Task<TenantDetail?> GetCurrentTenantAsync(Guid tenantId, CancellationToken ct = default);
}

public sealed record UserTenantListItem(Guid TenantId, string TenantName, string Role, string? Hostname = null);
public sealed record TenantDetail(Guid Id, string Name, DateTime CreatedAt);
