using Application.Interfaces;
using Domain.Enums;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts;

namespace Worker.Consumers;

public sealed class WafThreatEventConsumer : IConsumer<WafThreatEvent>
{
    private static readonly Guid SystemTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly IThreatLogService _threatLogService;
    private readonly Infrastructure.Persistence.SecurityDbContext _db;
    private readonly ILogger<WafThreatEventConsumer> _logger;

    public WafThreatEventConsumer(IThreatLogService threatLogService, Infrastructure.Persistence.SecurityDbContext db, ILogger<WafThreatEventConsumer> logger)
        => (_threatLogService, _db, _logger) = (threatLogService, db, logger);

    public async Task Consume(ConsumeContext<WafThreatEvent> context)
    {
        var item = context.Message;
        var tenantId = SystemTenantId;
        if (!string.IsNullOrWhiteSpace(item.TenantName))
        {
            var tenant = await _db.Tenants.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Name == item.TenantName);
            if (tenant is not null) tenantId = tenant.Id;
            else _logger.LogWarning("Unknown WAF tenant mapping {TenantName}; using system tenant", item.TenantName);
        }

        await _threatLogService.LogThreatAsync(tenantId, item.SourceIp, item.RequestPath, item.AttackType, SeverityLevel.High, item.RawPayload, "N/A");
        _logger.LogInformation("Saved WAF threat {EventId} as {AttackType} for tenant {TenantId}", item.EventId, item.AttackType, tenantId);
    }
}
