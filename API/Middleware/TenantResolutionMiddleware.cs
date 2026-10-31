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
        if (context.Request.Path.StartsWithSegments("/api/auth/login")
            || context.Request.Path.StartsWithSegments("/api/auth/available-tenants")
            || context.Request.Path.StartsWithSegments("/api/health"))
        {
            await _next(context);
            return;
        }

        var claimValue = context.User.FindFirstValue("tenant_id");
        var siteTenantClaim = context.User.FindFirstValue("site_tenant_id");
        var tokenHostname = context.User.FindFirstValue("hostname");
        var headerValue = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        var allowHeaderForTesting = _configuration.GetValue<bool>("TenantResolution:AllowHeaderForTesting");
        var hostname = context.Request.Host.Host.Trim().TrimEnd('.').ToLowerInvariant();
        var routeByProtectedSite = _configuration.GetValue<bool>("TenantResolution:UseProtectedSites");
        if (allowHeaderForTesting && !_environment.IsDevelopment())
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsync("The test tenant header option must not be enabled outside Development.");
            return;
        }

        Guid? protectedSiteTenantId = null;
        if (routeByProtectedSite)
        {
            var protectedSites = context.RequestServices.GetRequiredService<IProtectedSiteService>();
            protectedSiteTenantId = await protectedSites.ResolveTenantIdByHostnameAsync(hostname);
            if (context.User.Identity?.IsAuthenticated == true)
            {
                if (!Guid.TryParse(siteTenantClaim, out var tokenSiteTenantId)
                    || !string.Equals(tokenHostname, hostname, StringComparison.OrdinalIgnoreCase)
                    || !protectedSiteTenantId.HasValue
                    || tokenSiteTenantId != protectedSiteTenantId.Value)
                {
                    context.Response.StatusCode = StatusCodes.Status421MisdirectedRequest;
                    await context.Response.WriteAsync("The authenticated token is not bound to this protected-site hostname.");
                    return;
                }

                protectedSiteTenantId = tokenSiteTenantId;
            }
            else if (!protectedSiteTenantId.HasValue && !(allowHeaderForTesting && Guid.TryParse(headerValue, out _)))
            {
                context.Response.StatusCode = StatusCodes.Status421MisdirectedRequest;
                await context.Response.WriteAsync("The request hostname is not an enabled protected site.");
                return;
            }
        }

        Guid tenantId;
        if (protectedSiteTenantId.HasValue)
        {
            // The registered hostname determines the tenant for this request, not the JWT's selected tenant.
            tenantId = protectedSiteTenantId.Value;
        }
        else if (!string.IsNullOrWhiteSpace(claimValue))
        {
            if (!Guid.TryParse(claimValue, out tenantId))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("Invalid tenant claim.");
                return;
            }
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

        if (protectedSiteTenantId.HasValue && context.User.Identity?.IsAuthenticated == true)
        {
            if (!Guid.TryParse(claimValue, out var activeTenantId) || activeTenantId == Guid.Empty)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("The token does not contain a valid selected tenant.");
                return;
            }

            var userName = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub");
            var membershipRole = await validationDb.TenantMemberships.IgnoreQueryFilters().AsNoTracking()
                .Where(m => m.TenantId == activeTenantId && m.IsActive && m.UserAccount.UserName == userName)
                .Select(m => m.Role)
                .SingleOrDefaultAsync();

            if (membershipRole is null)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("You do not have an active membership in the selected tenant.");
                return;
            }

            var identity = (ClaimsIdentity)context.User.Identity;
            var isPlatformAdmin = context.User.HasClaim("role", "PlatformAdmin");
            foreach (var roleClaim in identity.FindAll("role").ToArray())
                identity.RemoveClaim(roleClaim);
            identity.AddClaim(new Claim("role", membershipRole));
            if (isPlatformAdmin)
                identity.AddClaim(new Claim("role", "PlatformAdmin"));
            tenantService.SetTenant(activeTenantId);
            context.Items["SiteTenantId"] = protectedSiteTenantId.Value;
            context.Items["ActiveTenantRole"] = membershipRole;
            await _next(context);
            return;
        }

        tenantService.SetTenant(tenantId);
        await _next(context);
    }
}
