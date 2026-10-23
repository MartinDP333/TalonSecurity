using System.Security.Claims;
using Application.Interfaces;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace API.Middleware;

public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public TenantResolutionMiddleware(RequestDelegate next, IWebHostEnvironment environment, IConfiguration configuration)
        => (_next, _environment, _configuration) = (next, environment, configuration);

    public async Task InvokeAsync(HttpContext context, ICurrentTenantService tenantService)
    {
        if (context.Request.Path.StartsWithSegments("/api/auth/login") || context.Request.Path.StartsWithSegments("/api/auth/available-tenants") || context.Request.Path.StartsWithSegments("/api/health"))
        {
            await _next(context);
            return;
        }

        var claimValue = context.User.FindFirstValue("tenant_id");
        var headerValue = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        var allowHeaderForTesting = _configuration.GetValue<bool>("TenantResolution:AllowHeaderForTesting");
        var hostname = context.Request.Host.Host.Trim().ToLowerInvariant();
        var routeByProtectedSite = _configuration.GetValue<bool>("TenantResolution:UseProtectedSites");
        if ((allowHeaderForTesting || routeByProtectedSite) && !_environment.IsDevelopment())
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsync("Development tenant routing options must not be enabled outside Development.");
            return;
        }

        Guid tenantId;
        if (!string.IsNullOrWhiteSpace(claimValue))
        {
            if (!Guid.TryParse(claimValue, out tenantId))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("Invalid tenant claim.");
                return;
            }
        }
        else
        {
            Guid? protectedSiteTenantId = null;
            if (routeByProtectedSite)
            {
                var protectedSites = context.RequestServices.GetRequiredService<IProtectedSiteService>();
                protectedSiteTenantId = await protectedSites.ResolveTenantIdByHostnameAsync(hostname);
            }

            if (protectedSiteTenantId.HasValue)
            {
                tenantId = protectedSiteTenantId.Value;
            }
            else if (allowHeaderForTesting && Guid.TryParse(headerValue, out var headerTenantId))
            {
                tenantId = headerTenantId;
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status421MisdirectedRequest;
                await context.Response.WriteAsync("No tenant claim, enabled protected-site hostname, or valid test tenant header was provided.");
                return;
            }
        }

        if (tenantId == Guid.Empty)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("A valid tenant context is required.");
            return;
        }

        var options = context.RequestServices.GetRequiredService<DbContextOptions<SecurityDbContext>>();
        await using var validationDb = new SecurityDbContext(options);
        if (!await validationDb.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == tenantId))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Tenant was not found.");
            return;
        }

        tenantService.SetTenant(tenantId);
        await _next(context);
    }
}
