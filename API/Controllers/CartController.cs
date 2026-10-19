using Microsoft.AspNetCore.Mvc;
using MassTransit;
using Shared.Contracts;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CartController : ControllerBase
    {
        private readonly IPublishEndpoint _publishEndpoint;

        public CartController(IPublishEndpoint publishEndpoint)
        {
            _publishEndpoint = publishEndpoint;
        }

        [HttpPost("add")]
        public async Task<IActionResult> AddToCart([FromBody] string item)
        {
            string? clientIp = "Ukendt";

            if (Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedHeader))
            {
                clientIp = forwardedHeader.FirstOrDefault();
            }
            else if (HttpContext.Connection.RemoteIpAddress != null)
            {
                clientIp = HttpContext.Connection.RemoteIpAddress.ToString();
            }

            string userAgent = Request.Headers["User-Agent"].ToString();

            var trafficEvent = new TrafficEvent
            {
                IpAddress = clientIp ?? "Ukendt",
                Endpoint = "/api/cart/add",
                UserAgent = string.IsNullOrEmpty(userAgent) ? "Ukendt" : userAgent,
                Timestamp = DateTime.UtcNow
            };

            await _publishEndpoint.Publish(trafficEvent);

            Console.WriteLine($"[API] Trafik hændelse sendt til RabbitMQ for IP: {clientIp}");

            return Ok(new { Message = $"Vare '{item}' tilføjet", Ip = clientIp });
        }
    }
}