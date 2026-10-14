using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers();

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