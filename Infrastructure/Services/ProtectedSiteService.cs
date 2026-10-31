using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class ProtectedSiteService : IProtectedSiteService
{
    private readonly SecurityDbContext _db;

    public ProtectedSiteService(SecurityDbContext db)
    {
        _db = db;
    }

    public async Task<string?> GetHostnameForTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.ProtectedSites.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.IsEnabled && x.TenantId == tenantId)
            .OrderBy(x => x.Hostname)
            .Select(x => x.Hostname)
            .FirstOrDefaultAsync(ct);

    public async Task<Guid?> ResolveTenantIdByHostnameAsync(string hostname, CancellationToken ct = default)
    {
        var normalized = NormalizeHostname(hostname);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        return await _db.ProtectedSites.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.IsEnabled && x.Hostname == normalized)
            .Select(x => (Guid?)x.TenantId)
            .SingleOrDefaultAsync(ct);
    }

    public async Task<bool> IsHostnameRegisteredAsync(string hostname, CancellationToken ct = default)
    {
        var normalized = NormalizeHostname(hostname);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        return await _db.ProtectedSites.IgnoreQueryFilters().AnyAsync(x => x.Hostname == normalized, ct);
    }

    public async Task<IReadOnlyList<ProtectedSite>> ListForTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.ProtectedSites.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Hostname)
            .ToListAsync(ct);

    public async Task<ProtectedSite?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.ProtectedSites.SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task<ProtectedSite> AddAsync(Guid tenantId, string hostname, string originUrl, CancellationToken ct = default)
    {
        var normalizedHostname = NormalizeHostname(hostname);
        if (string.IsNullOrWhiteSpace(normalizedHostname))
            throw new ArgumentException("A valid hostname is required.", nameof(hostname));

        if (await IsHostnameRegisteredAsync(normalizedHostname, ct))
            throw new InvalidOperationException("Hostname is already registered.");

        var site = new ProtectedSite(tenantId, normalizedHostname, originUrl);
        _db.ProtectedSites.Add(site);
        await _db.SaveChangesAsync(ct);
        return site;
    }

    public async Task DisableAsync(Guid id, CancellationToken ct = default)
    {
        var site = await GetByIdAsync(id, ct);
        if (site is null)
            throw new InvalidOperationException("Protected site was not found.");

        site.Disable();
        await _db.SaveChangesAsync(ct);
    }

    private static string NormalizeHostname(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Trim();
        if (Uri.TryCreate(normalized, UriKind.Absolute, out var absolute))
            normalized = absolute.Host;
        else if (normalized.Contains(':', StringComparison.Ordinal))
        {
            var lastColon = normalized.LastIndexOf(':');
            if (lastColon > normalized.LastIndexOf('/'))
                normalized = normalized[..lastColon];
        }

        return normalized.Trim().TrimEnd('.').ToLowerInvariant();
    }
}
