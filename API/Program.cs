using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using Infrastructure.Consumers;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers();

builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        var hostName = Environment.GetEnvironmentVariable("RabbitMQ__HostName") ?? "rabbitmq";
        var userName = Environment.GetEnvironmentVariable("RabbitMQ__UserName") ?? "guest";
        var password = Environment.GetEnvironmentVariable("RabbitMQ__Password") ?? "guest";

        cfg.Host(hostName, "/", h =>
        {
            h.Username(userName);
            h.Password(password);
        });

        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();

// TEST ENDPOINT
app.MapGet("/api/health", () => new {
    Status = "Healthy",
    Message = "Talon API kører perfekt i Kubernetes!",
    Timestamp = DateTime.UtcNow
});

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<SecurityDbContext>();
    await context.Database.MigrateAsync();
}

app.Run();