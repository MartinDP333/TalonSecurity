using System.Net;
using API.Middleware;
using Infrastructure.Persistence;
using MassTransit;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers();

builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        var hostName = builder.Configuration["RabbitMQ:HostName"]
                       ?? Environment.GetEnvironmentVariable("RabbitMQ__HostName")
                       ?? "rabbitmq";
        var userName = builder.Configuration["RabbitMQ:UserName"]
                       ?? Environment.GetEnvironmentVariable("RabbitMQ__UserName")
                       ?? "guest";
        var password = builder.Configuration["RabbitMQ:Password"]
                       ?? Environment.GetEnvironmentVariable("RabbitMQ__Password")
                       ?? "guest";

        cfg.Host(hostName, "/", h =>
        {
            h.Username(userName);
            h.Password(password);
        });

        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Add(
        System.Net.IPNetwork.Parse("10.244.0.0/24"));
    options.ForwardLimit = 1;
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseMiddleware<IpBlockingMiddleware>();
app.UseMiddleware<TrafficTrackingMiddleware>();

app.MapGet("/api/health", () => new
{
    Status = "Healthy",
    Message = "Talon API kører i Kubernetes!",
    Timestamp = DateTime.UtcNow
});

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<SecurityDbContext>();
    await context.Database.MigrateAsync();
}

app.Run();
