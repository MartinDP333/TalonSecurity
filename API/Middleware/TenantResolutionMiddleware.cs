using System.Security.Claims;
using Application.Interfaces;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace API.Middleware;

public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _environment;

    public TenantResolutionMiddleware(RequestDelegate next, IWebHostEnvironment environment) => (_next, _environment) = (next, environment);

    public async Task InvokeAsync(HttpContext context, ICurrentTenantService tenantService)
    {
        if (context.Request.Path.StartsWithSegments("/api/auth/login") || context.Request.Path.StartsWithSegments("/api/auth/available-tenants") || context.Request.Path.StartsWithSegments("/api/health"))
        {
            await _next(context);
            return;
        }

        var claimValue = context.User.FindFirstValue("tenant_id");
        var headerValue = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        var value = claimValue ?? (_environment.IsDevelopment() ? headerValue : null);

        if (!Guid.TryParse(value, out var tenantId) || tenantId == Guid.Empty)
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
