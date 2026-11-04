using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using API.Services;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace Isolation.Tests;

public sealed class TenantIsolationApiFactory : WebApplicationFactory<API.ApiAssemblyMarker>
{
    public static readonly Guid AlphaId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid BetaId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public const string JwtSecret = "tenant-isolation-integration-test-signing-key-32-bytes-minimum";

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    public CurrentTenantService TenantContext { get; } = new();
    public SecurityDbContext Database { get; private set; } = null!;

    public TenantIsolationApiFactory()
    {
        _connection.Open();
        var options = new DbContextOptionsBuilder<SecurityDbContext>().UseSqlite(_connection).Options;
        Database = new SecurityDbContext(options, TenantContext);
        Database.Database.EnsureCreated();

        var alpha = new Tenant(AlphaId, "Alpha");
        var beta = new Tenant(BetaId, "Beta");
        var alphaUser = new UserAccount("alpha-user", "not-used", isPlatformAdmin: true);
        var betaUser = new UserAccount("beta-user", "not-used");
        Database.Tenants.AddRange(alpha, beta);
        Database.UserAccounts.AddRange(alphaUser, betaUser);
        Database.TenantMemberships.AddRange(
            new TenantMembership(AlphaId, alphaUser.Id, "Admin"),
            new TenantMembership(BetaId, alphaUser.Id, "Admin"),
            new TenantMembership(BetaId, betaUser.Id, "Viewer"));
        Database.ProtectedSites.AddRange(
            new ProtectedSite(AlphaId, "webshop.talon.test", "http://webshop.talon.test"),
            new ProtectedSite(BetaId, "bank.talon.test", "http://bank.talon.test"));
        Database.ThreatLogs.AddRange(
            new ThreatLog(AlphaId, "198.51.100.10", "/alpha-only", "Alpha finding", SeverityLevel.High, "alpha payload", "ZZ"),
            new ThreatLog(BetaId, "198.51.100.20", "/beta-only", "Beta finding", SeverityLevel.High, "beta payload", "ZZ"));
        Database.SecurityAuditEvents.AddRange(
            new SecurityAuditEvent(AlphaId, "alpha-user", "AlphaAudit", "Test", "GET", "/alpha", "198.51.100.10", "ZZ", 200, "alpha audit"),
            new SecurityAuditEvent(BetaId, "beta-user", "BetaAudit", "Test", "GET", "/beta", "198.51.100.20", "ZZ", 200, "beta audit"));
        TenantContext.UseTenant(AlphaId);
        using (TenantContext.AllowCrossTenantWrites())
            Database.SaveChanges();
        Database.ChangeTracker.Clear();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Data Source=:memory:",
            ["Jwt:Key"] = JwtSecret,
            ["TenantResolution:UseProtectedSites"] = "true",
            ["TenantResolution:AllowHeaderForTesting"] = "false",
            ["GeoIP:DatabasePath"] = string.Empty,
            ["Database:SkipMigrations"] = "true",
            ["Bootstrap:SkipSeeding"] = "true"
        }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<DbContextOptions<SecurityDbContext>>();
            services.RemoveAll<SecurityDbContext>();
            var sqliteOptions = new DbContextOptionsBuilder<SecurityDbContext>().UseSqlite(_connection)
                .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning)).Options;
            services.AddSingleton(sqliteOptions);
            services.AddScoped(_ => new SecurityDbContext(sqliteOptions, TenantContext));
            services.RemoveAll<ICurrentTenantService>();
            services.AddSingleton<ICurrentTenantService>(TenantContext);
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret)),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    NameClaimType = "sub",
                    RoleClaimType = "role",
                    ClockSkew = TimeSpan.Zero
                };
            });
        });
    }

    public static string CreateToken(string userName, Guid selectedTenantId, Guid siteTenantId, string hostname, string role = "Admin", bool platformAdmin = false)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userName),
            new(ClaimTypes.NameIdentifier, userName),
            new("tenant_id", selectedTenantId.ToString()),
            new("site_tenant_id", siteTenantId.ToString()),
            new("hostname", hostname),
            new("role", role)
        };
        if (platformAdmin)
            claims.Add(new Claim("role", "PlatformAdmin"));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var token = new JwtSecurityToken(claims: claims, expires: DateTime.UtcNow.AddMinutes(10), signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Database.Dispose();
            _connection.Dispose();
        }
        base.Dispose(disposing);
    }
}
