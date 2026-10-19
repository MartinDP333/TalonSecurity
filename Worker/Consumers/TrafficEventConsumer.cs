using Domain.Entities;
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

    public TrafficEventConsumer(
        ILogger<TrafficEventConsumer> logger,
        IConnectionMultiplexer redis,
        SecurityDbContext dbContext)
    {
        _logger = logger;
        _redis = redis;
        _dbContext = dbContext;
    }

    public async Task Consume(ConsumeContext<TrafficEvent> context)
    {
        var data = context.Message;
        var db = _redis.GetDatabase();
        var redisKey = $"ip:{data.IpAddress}:requests";

        var requestCount = await db.StringIncrementAsync(redisKey);
        if (requestCount == 1)
            await db.KeyExpireAsync(redisKey, TimeSpan.FromSeconds(60));

        var isSuspicious = requestCount > 10;
        _logger.LogInformation(
            "[REDIS CHECK] IP: {IpAddress} | Antal requests (sidste minut): {RequestCount} | Mistænkelig: {IsSuspicious}",
            data.IpAddress, requestCount, isSuspicious);

        var systemTenant = await _dbContext.Tenants.FindAsync(SystemTenantId);
        if (systemTenant == null)
        {
            systemTenant = new Tenant(SystemTenantId, "System");
            _dbContext.Tenants.Add(systemTenant);
            await _dbContext.SaveChangesAsync();
        }

        var reputation = await _dbContext.IpReputations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.TenantId == SystemTenantId && r.IpAddress == data.IpAddress);

        if (reputation == null)
        {
            reputation = new IpReputation(SystemTenantId, data.IpAddress, isSuspicious ? 50 : 10);
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
        }

        try
        {
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Ændringer gemt i databasen for IP: {IpAddress}", data.IpAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FEJL ved gemning til database for IP: {IpAddress}", data.IpAddress);
            throw;
        }
    }
}
