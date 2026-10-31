using Domain.Entities;

namespace Application.Interfaces;

public interface IAuthenticationService
{
    Task<UserAccount?> GetUserByNameAsync(string userName, CancellationToken ct = default);
    Task<UserAccount?> ValidateUserAsync(string userName, string password, CancellationToken ct = default);
    Task<TenantMembership?> GetActiveMembershipAsync(Guid userId, string tenantName, CancellationToken ct = default);
    Task<bool> HasActiveMembershipAsync(Guid userId, Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<TenantMembershipSummary>> GetAvailableTenantsAsync(string userName, string password, CancellationToken ct = default);
    Task<TokenResult> CreateTokenAsync(UserAccount user, TenantMembership membership, string hostname, Guid siteTenantId);
}

public sealed record TenantMembershipSummary(Guid TenantId, string TenantName, string Role, string? Hostname = null);
public sealed record TokenResult(string AccessToken, Guid TenantId, string TenantName, string Role, bool IsPlatformAdmin, string Hostname, Guid SiteTenantId);
