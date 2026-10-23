using Application.Interfaces;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
