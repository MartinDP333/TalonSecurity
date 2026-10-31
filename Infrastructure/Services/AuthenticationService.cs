using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Services;

public sealed class AuthenticationService : IAuthenticationService
{
    private readonly SecurityDbContext _db;
    private readonly IConfiguration _configuration;

    public AuthenticationService(SecurityDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public async Task<UserAccount?> GetUserByNameAsync(string userName, CancellationToken ct = default)
        => await _db.UserAccounts.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.UserName == userName, ct);

    public async Task<UserAccount?> ValidateUserAsync(string userName, string password, CancellationToken ct = default)
    {
        var user = await GetUserByNameAsync(userName, ct);
        if (user is null || !PasswordService.Verify(password, user.PasswordHash))
            return null;

        return user;
    }

public async Task<TenantMembership?> GetActiveMembershipAsync(Guid userId, string tenantName, CancellationToken ct = default)
        => await _db.TenantMemberships.IgnoreQueryFilters().Include(x => x.Tenant)
            .SingleOrDefaultAsync(x => x.UserAccountId == userId && x.Tenant.Name == tenantName && x.IsActive, ct);

    public Task<bool> HasActiveMembershipAsync(Guid userId, Guid tenantId, CancellationToken ct = default)
        => _db.TenantMemberships.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(m => m.UserAccountId == userId && m.TenantId == tenantId && m.IsActive, ct);

    public async Task<IReadOnlyList<TenantMembershipSummary>> GetAvailableTenantsAsync(string userName, string password, CancellationToken ct = default)
    {
        var user = await ValidateUserAsync(userName, password, ct);
        if (user is null)
            return Array.Empty<TenantMembershipSummary>();

        var memberships = await _db.TenantMemberships.IgnoreQueryFilters()
            .Where(x => x.UserAccountId == user.Id && x.IsActive)
            .OrderBy(x => x.Tenant.Name)
            .Select(x => new TenantMembershipSummary(x.TenantId, x.Tenant.Name, x.Role))
            .ToListAsync(ct);

        var tenantIds = memberships.Select(x => x.TenantId).ToList();
        var sites = await _db.ProtectedSites.IgnoreQueryFilters().AsNoTracking()
            .Where(site => site.IsEnabled && tenantIds.Contains(site.TenantId))
            .OrderBy(site => site.Hostname)
            .Select(site => new { site.TenantId, site.Hostname })
            .ToListAsync(ct);

        return memberships.Select(membership => membership with
        {
            Hostname = sites.FirstOrDefault(site => site.TenantId == membership.TenantId)?.Hostname
        }).ToList();
    }

public async Task<TokenResult> CreateTokenAsync(UserAccount user, TenantMembership membership, string hostname, Guid siteTenantId)
    {
        var key = _configuration["Jwt:Key"] ?? Environment.GetEnvironmentVariable("JWT_KEY") ?? throw new InvalidOperationException("JWT_KEY is missing.");
        hostname = NormalizeHostname(hostname);
        if (string.IsNullOrWhiteSpace(hostname) || siteTenantId == Guid.Empty)
            throw new InvalidOperationException("A registered protected-site hostname and tenant are required to issue a token.");

        var registeredSiteTenantId = await _db.ProtectedSites.IgnoreQueryFilters().AsNoTracking()
            .Where(site => site.IsEnabled && site.Hostname == hostname)
            .Select(site => (Guid?)site.TenantId)
            .SingleOrDefaultAsync();
        if (registeredSiteTenantId != siteTenantId)
            throw new InvalidOperationException("The protected-site hostname does not match its registered tenant.");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserName),
            new(ClaimTypes.NameIdentifier, user.UserName),
            new("tenant_id", membership.TenantId.ToString()),
            new("role", membership.Role),
            new("hostname", hostname),
            new("site_tenant_id", siteTenantId.ToString())
        };

        if (user.IsPlatformAdmin)
            claims.Add(new Claim("role", "PlatformAdmin"));

        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(claims: claims, expires: DateTime.UtcNow.AddHours(8), signingCredentials: credentials);

        return new TokenResult(
            new JwtSecurityTokenHandler().WriteToken(token),
            membership.TenantId,
            membership.Tenant.Name,
            membership.Role,
            user.IsPlatformAdmin,
            hostname,
            siteTenantId);
    }

    private static string NormalizeHostname(string hostname)
    {
        var normalized = hostname.Trim();
        if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
            normalized = uri.Host;
        else if (normalized.Contains(':'))
            normalized = normalized[..normalized.LastIndexOf(':')];
        return normalized.TrimEnd('.').ToLowerInvariant();
    }
}
