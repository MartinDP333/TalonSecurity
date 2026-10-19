using API;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace API.Middleware;

public class IpBlockingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<IpBlockingMiddleware> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public IpBlockingMiddleware(RequestDelegate next, ILogger<IpBlockingMiddleware> logger, IServiceScopeFactory scopeFactory)
    {
        _next = next;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var ipAddress = ClientIpAddress.Get(context);

        if (!string.IsNullOrEmpty(ipAddress))
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SecurityDbContext>();
            var reputation = await dbContext.IpReputations
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.IpAddress == ipAddress);

            if (reputation != null)
            {
                if (reputation.CheckAndLiftBlockIfExpired())
                    await dbContext.SaveChangesAsync();
                else if (reputation.IsBlocked)
                {
                    _logger.LogWarning("[ACCESS DENIED] Blocked IP {IpAddress} attempted access", ipAddress);
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsync("Access denied: IP temporarily blocked.");
                    return;
                }
            }
        }

        await _next(context);
    }
}
