using System.Text;
using API.Middleware;
using API.Services;
using Application.Interfaces;
using Infrastructure.Persistence;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddScoped<ICurrentTenantService, CurrentTenantService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<SecurityAuditLogger>();

var jwtKey = builder.Configuration["Jwt:Key"] ?? Environment.GetEnvironmentVariable("JWT_KEY") ?? "";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey.Length >= 32 ? jwtKey : "development-only-key-change-me-32-chars")),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.CustomSchemaIds(type => type.FullName?.Replace("+", ".") ?? type.Name);
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "TalonSecurity API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the access token from POST /api/auth/login. Do not include the word Bearer."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
    });
});

builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        var hostName = builder.Configuration["RabbitMQ:HostName"] ?? Environment.GetEnvironmentVariable("RabbitMQ__HostName") ?? "rabbitmq";
        var userName = builder.Configuration["RabbitMQ:UserName"] ?? Environment.GetEnvironmentVariable("RabbitMQ__UserName") ?? "guest";
        var password = builder.Configuration["RabbitMQ:Password"] ?? Environment.GetEnvironmentVariable("RabbitMQ__Password") ?? "guest";
        cfg.Host(hostName, "/", h => { h.Username(userName); h.Password(password); });
        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("10.244.0.0/24"));
    if (builder.Environment.IsDevelopment())
        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("172.16.0.0/12"));
    options.ForwardLimit = 1;
});

var app = builder.Build();
app.UseForwardedHeaders();
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "TalonSecurity API v1");
    options.RoutePrefix = "swagger";
});
app.UseAuthentication();
app.UseMiddleware<SecurityAuditMiddleware>();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();
app.UseMiddleware<IpBlockingMiddleware>();
app.UseMiddleware<TrafficTrackingMiddleware>();

app.MapGet("/api/health", () => new { Status = "Healthy", Message = "Talon API kører i Kubernetes!", Timestamp = DateTime.UtcNow }).AllowAnonymous();
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger")).AllowAnonymous();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<SecurityDbContext>();
    await context.Database.MigrateAsync();
}

await BootstrapDataSeeder.SeedAsync(app.Services, app.Configuration, app.Logger);

app.Run();
