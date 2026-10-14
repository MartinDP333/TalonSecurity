using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Messages;

namespace Infrastructure.Consumers;

public class MessageReceivedConsumer : IConsumer<MessageReceivedEvent>
{
    private readonly ILogger<MessageReceivedConsumer> _logger;

    public MessageReceivedConsumer(ILogger<MessageReceivedConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<MessageReceivedEvent> context)
    {
        _logger.LogInformation("[MASSTRANSIT] Modtaget besked: {Text}", context.Message.Text);

        // TODO: Gem i database

        await Task.CompletedTask;
    }
}