using Microsoft.AspNetCore.Mvc;
using MassTransit;
using Shared.Messages;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MessagingController : ControllerBase
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<MessagingController> _logger;

    public MessagingController(IPublishEndpoint publishEndpoint, ILogger<MessagingController> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendMessage([FromBody] string message)
    {
        _logger.LogInformation("[MASSTRANSIT] SendMessage endpoint blev ramt");

        try
        {
            var textToSend = message ?? "Tom besked";

            await _publishEndpoint.Publish(new MessageReceivedEvent(textToSend));

            _logger.LogInformation("[MASSTRANSIT] Besked sendt succesfuldt via MassTransit");
            return Ok(new { Success = true, Message = $"Besked sendt: {textToSend}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MASSTRANSIT ERROR] {ExceptionType}: {Message}", ex.GetType().Name, ex.Message);
            return StatusCode(500, new { Error = ex.Message, Type = ex.GetType().Name });
        }
    }
}