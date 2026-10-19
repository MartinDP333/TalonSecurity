using API;
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

    public async Task InvokeAsync(HttpContext context, IPublishEndpoint publishEndpoint)
    {
        var ipAddress = ClientIpAddress.Get(context);
        if (!string.IsNullOrEmpty(ipAddress))
        {
            await publishEndpoint.Publish(new TrafficEvent
            {
                IpAddress = ipAddress,
                Endpoint = context.Request.Path,
                UserAgent = context.Request.Headers.UserAgent.ToString() ?? "Ukendt",
                Timestamp = DateTime.UtcNow
            });
        }

        await _next(context);
    }
}
