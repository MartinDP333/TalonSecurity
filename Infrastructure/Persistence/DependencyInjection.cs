using Application.Interfaces;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
                               ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<SecurityDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IIpReputationRepository, IpReputationRepository>();
        services.AddSingleton(sp => new GeoIpCountryLookup(
            sp.GetRequiredService<ILogger<GeoIpCountryLookup>>(),
            configuration["GeoIP:DatabasePath"] ?? Environment.GetEnvironmentVariable("GeoIP__DatabasePath")));
        services.AddScoped<IThreatLogService, ThreatLogService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IProtectedSiteService, ProtectedSiteService>();
        services.AddScoped<IPlatformAdminService, TenantManagementService>();
        services.AddScoped<ITenantCatalogService, TenantManagementService>();
        services.AddScoped<IMembershipService, MembershipService>();
        services.AddScoped<IThreatTelemetryService, ThreatTelemetryService>();

        return services;
    }
}
