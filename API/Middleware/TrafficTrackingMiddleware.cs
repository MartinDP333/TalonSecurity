using API;
using Application.Interfaces;
using MassTransit;
using Shared.Contracts;

namespace API.Middleware;

public class TrafficTrackingMiddleware
{
    private readonly RequestDelegate _next;

    public TrafficTrackingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IPublishEndpoint publishEndpoint, ICurrentTenantService tenant)
    {
        //if (ClientIpAddress.IsInternal(context))
        //{
        //    await _next(context);
        //    return;
        //}

        var ipAddress = ClientIpAddress.Get(context);
        if (!string.IsNullOrEmpty(ipAddress))
        {
            await publishEndpoint.Publish(new TrafficEvent
            {
                IpAddress = ipAddress,
                TenantId = tenant.TenantId,
                SiteTenantId = context.Items.TryGetValue("SiteTenantId", out var siteTenantId)
                    && siteTenantId is Guid resolvedSiteTenantId
                        ? resolvedSiteTenantId
                        : tenant.TenantId,
                Hostname = context.Request.Host.Host,
                Endpoint = context.Request.Path,
                UserAgent = context.Request.Headers.UserAgent.ToString() ?? "Ukendt",
                Timestamp = DateTime.UtcNow
            });
        }

        await _next(context);
    }
}
