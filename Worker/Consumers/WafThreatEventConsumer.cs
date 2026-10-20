using Application.Interfaces;
using Domain.Enums;
using MassTransit;
using Shared.Contracts;

namespace Worker.Consumers;

public sealed class WafThreatEventConsumer : IConsumer<WafThreatEvent>
{
    private static readonly Guid SystemTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly IThreatLogService _threatLogService;
    private readonly ILogger<WafThreatEventConsumer> _logger;

    public WafThreatEventConsumer(IThreatLogService threatLogService, ILogger<WafThreatEventConsumer> logger)
        => (_threatLogService, _logger) = (threatLogService, logger);

    public async Task Consume(ConsumeContext<WafThreatEvent> context)
    {
        var item = context.Message;
        await _threatLogService.LogThreatAsync(SystemTenantId, item.SourceIp, item.RequestPath, item.AttackType, SeverityLevel.High, item.RawPayload, "N/A");
        _logger.LogInformation("Saved WAF threat {EventId} as {AttackType}", item.EventId, item.AttackType);
    }
}
