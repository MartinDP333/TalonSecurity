using Application.Interfaces; // <-- Husk denne
using Domain.Entities;
using Domain.Enums;           // <-- Og denne
using Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Contracts;
using StackExchange.Redis;

namespace Worker.Consumers;

public class TrafficEventConsumer : IConsumer<TrafficEvent>
{
    private static readonly Guid SystemTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly ILogger<TrafficEventConsumer> _logger;
    private readonly IConnectionMultiplexer _redis;
    private readonly SecurityDbContext _dbContext;
    private readonly IThreatLogService _threatLogService;

    public TrafficEventConsumer(
        ILogger<TrafficEventConsumer> logger,
        IConnectionMultiplexer redis,
        SecurityDbContext dbContext,
        IThreatLogService threatLogService)
    {
        _logger = logger;
        _redis = redis;
        _dbContext = dbContext;
        _threatLogService = threatLogService;
    }

    public async Task Consume(ConsumeContext<TrafficEvent> context)
    {
        var data = context.Message;
        var db = _redis.GetDatabase();
        var tenantId = data.SiteTenantId ?? data.TenantId;
        if (!tenantId.HasValue && !string.IsNullOrWhiteSpace(data.Hostname))
        {
            tenantId = await _dbContext.ProtectedSites.IgnoreQueryFilters().AsNoTracking()
                .Where(site => site.IsEnabled && site.Hostname == data.Hostname.Trim().ToLower())
                .Select(site => (Guid?)site.TenantId)
                .SingleOrDefaultAsync();
        }
        var resolvedTenantId = tenantId ?? SystemTenantId;
        var redisKey = $"tenant:{resolvedTenantId}:ip:{data.IpAddress}:requests";

        var requestCount = await db.StringIncrementAsync(redisKey);
        if (requestCount == 1)
            await db.KeyExpireAsync(redisKey, TimeSpan.FromSeconds(60));

        var isSuspicious = requestCount > 60;
        _logger.LogInformation(
            "[REDIS CHECK] IP: {IpAddress} | Antal requests (sidste minut): {RequestCount} | Mistænkelig: {IsSuspicious}",
            data.IpAddress, requestCount, isSuspicious);

        await _dbContext.Database.ExecuteSqlInterpolatedAsync($@"
IF NOT EXISTS (SELECT 1 FROM dbo.Tenants WITH (UPDLOCK, HOLDLOCK) WHERE Id = {SystemTenantId})
    INSERT INTO dbo.Tenants (Id, Name, CreatedAt)
    VALUES ({SystemTenantId}, N'System', SYSUTCDATETIME());");

        var reputation = await _dbContext.IpReputations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.TenantId == resolvedTenantId && r.IpAddress == data.IpAddress);

        if (reputation == null)
        {
            reputation = new IpReputation(resolvedTenantId, data.IpAddress, isSuspicious ? 50 : 10);
            _dbContext.IpReputations.Add(reputation);
        }
        else if (reputation.CheckAndLiftBlockIfExpired())
        {
            _logger.LogInformation("Blokering udløbet for IP: {IpAddress}. Frikendt.", data.IpAddress);
        }

        if (reputation.IsBlocked)
        {
            _logger.LogWarning(
                "[BLOCKED] Blokeret IP forsøgte at få adgang: {IpAddress}. Blokeret indtil: {BlockedUntil}",
                data.IpAddress, reputation.BlockedUntil);
            return;
        }

        if (isSuspicious)
        {
            _logger.LogWarning(
                "[SIKKERHEDSADVARSEL] IP {IpAddress} spammer systemet! Aktiverer progressiv blokering...",
                data.IpAddress);
            reputation.ApplyProgressiveBlock();

            await _threatLogService.LogThreatAsync(
                tenantId: resolvedTenantId,
                sourceIp: data.IpAddress,
                requestPath: data.Endpoint ?? "/api/cart/add",
                attackType: "RateLimit / Spam",
                severity: SeverityLevel.High,
                rawPayload: data.UserAgent ?? "Ukendt"
            );
        }

        try
        {
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Changes saved for tenant {TenantId}, IP {IpAddress}", resolvedTenantId, data.IpAddress);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex) && reputation is not null)
        {
            // Concurrent first requests can race to insert the same tenant/IP reputation.
            // ThreatLogService has already persisted the rate-limit threat independently.
            _dbContext.Entry(reputation).State = EntityState.Detached;
            _logger.LogWarning("Concurrent IP reputation insert for tenant {TenantId}, IP {IpAddress}; rate-limit threat remains recorded.", resolvedTenantId, data.IpAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to save traffic state for tenant {TenantId}, IP {IpAddress}", resolvedTenantId, data.IpAddress);
            throw;
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("unique index", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}