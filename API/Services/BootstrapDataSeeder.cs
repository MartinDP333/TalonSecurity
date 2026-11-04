using System.Data;
using Application.Services;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace API.Services;

public static class BootstrapDataSeeder
{
    private static readonly Guid AlphaTenantId = Guid.Parse("A6CFC055-077E-416C-A2A8-74691DA2F0CD");
    private static readonly Guid BetaTenantId = Guid.Parse("1D339916-E0B5-4B22-9A50-CB0920BEE240");

    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, ILogger logger, CancellationToken cancellationToken = default)
    {
        var platformAdminPassword = configuration["Bootstrap:PlatformAdminPassword"];
        var webshopUserPassword = configuration["Bootstrap:WebshopUserPassword"];
        var bankUserPassword = configuration["Bootstrap:BankUserPassword"];
        var passwords = new[] { platformAdminPassword, webshopUserPassword, bankUserPassword };

        if (passwords.All(string.IsNullOrWhiteSpace))
        {
            logger.LogWarning("Bootstrap seeding skipped because bootstrap passwords are not configured.");
            return;
        }

        if (passwords.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Configure all three bootstrap passwords before seeding platform-admin, webshop-user, and bank-user.");

        foreach (var (username, password) in new[]
        {
            ("platform-admin", platformAdminPassword!),
            ("webshop-user", webshopUserPassword!),
            ("bank-user", bankUserPassword!)
        })
        {
            if (password.Length < 12)
                throw new InvalidOperationException($"The configured bootstrap password for {username} must contain at least 12 characters.");
        }

        await using var scope = services.CreateAsyncScope();
        var tenantService = scope.ServiceProvider.GetRequiredService<Application.Interfaces.ICurrentTenantService>();
        using var crossTenantWrites = tenantService.AllowCrossTenantWrites();
        using var seedTenant = tenantService.UseTenant(AlphaTenantId);
        var db = scope.ServiceProvider.GetRequiredService<SecurityDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var alpha = await GetOrCreateTenantAsync(db, AlphaTenantId, "Talon Security Alpha", cancellationToken);
        var beta = await GetOrCreateTenantAsync(db, BetaTenantId, "Talon Security Beta", cancellationToken);

        var platformAdmin = await GetOrCreateUserAsync(db, "platform-admin", platformAdminPassword!, isPlatformAdmin: true, cancellationToken);
        var webshopUser = await GetOrCreateUserAsync(db, "webshop-user", webshopUserPassword!, isPlatformAdmin: false, cancellationToken);
        var bankUser = await GetOrCreateUserAsync(db, "bank-user", bankUserPassword!, isPlatformAdmin: false, cancellationToken);

        await EnsureMembershipAsync(db, alpha, platformAdmin, "Admin", cancellationToken);
        await EnsureMembershipAsync(db, beta, platformAdmin, "Admin", cancellationToken);
        await EnsureMembershipAsync(db, alpha, webshopUser, "Viewer", cancellationToken);
        await EnsureMembershipAsync(db, beta, bankUser, "Viewer", cancellationToken);

        await EnsureProtectedSiteAsync(db, alpha, "webshop.talon.test", "http://webshop.talon.test", cancellationToken);
        await EnsureProtectedSiteAsync(db, beta, "bank.talon.test", "http://bank.talon.test", cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Bootstrap seed is present: three accounts, Alpha/Beta tenants, four memberships, and two protected sites.");
    }

    private static async Task<Tenant> GetOrCreateTenantAsync(SecurityDbContext db, Guid id, string name, CancellationToken ct)
    {
        var tenant = await db.Tenants.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (tenant is not null)
            return tenant;

        tenant = await db.Tenants.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Name == name, ct);
        if (tenant is not null)
            return tenant;

        tenant = new Tenant(id, name);
        db.Tenants.Add(tenant);
        return tenant;
    }

    private static async Task<UserAccount> GetOrCreateUserAsync(SecurityDbContext db, string username, string password, bool isPlatformAdmin, CancellationToken ct)
    {
        var user = await db.UserAccounts.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.UserName == username, ct);
        if (user is not null)
            return user;

        user = new UserAccount(username, PasswordService.Hash(password), isPlatformAdmin);
        db.UserAccounts.Add(user);
        return user;
    }

    private static async Task EnsureMembershipAsync(SecurityDbContext db, Tenant tenant, UserAccount user, string role, CancellationToken ct)
    {
        var membership = await db.TenantMemberships.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.TenantId == tenant.Id && x.UserAccountId == user.Id, ct);
        if (membership is null)
        {
            db.TenantMemberships.Add(new TenantMembership(tenant.Id, user.Id, role));
            return;
        }

        membership.SetRole(role);
        if (!membership.IsActive)
            membership.Activate();
    }

    private static async Task EnsureProtectedSiteAsync(SecurityDbContext db, Tenant tenant, string hostname, string originUrl, CancellationToken ct)
    {
        var site = await db.ProtectedSites.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Hostname == hostname, ct);
        if (site is null)
        {
            db.ProtectedSites.Add(new ProtectedSite(tenant.Id, hostname, originUrl));
            return;
        }

        if (site.TenantId != tenant.Id)
            throw new InvalidOperationException($"Protected hostname {hostname} is already assigned to a different tenant.");

        if (!site.IsEnabled)
            site.Enable();
    }
}
