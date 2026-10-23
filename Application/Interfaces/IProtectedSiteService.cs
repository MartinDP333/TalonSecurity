using Domain.Entities;

namespace Application.Interfaces;

public interface IProtectedSiteService
{
    Task<Guid?> ResolveTenantIdByHostnameAsync(string hostname, CancellationToken ct = default);
    Task<bool> IsHostnameRegisteredAsync(string hostname, CancellationToken ct = default);
    Task<IReadOnlyList<ProtectedSite>> ListForTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<ProtectedSite?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ProtectedSite> AddAsync(Guid tenantId, string hostname, string originUrl, CancellationToken ct = default);
    Task DisableAsync(Guid id, CancellationToken ct = default);
}
