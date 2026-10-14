using RabbitMQ.Client;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MessagingController : ControllerBase
{
    [HttpPost("send")]
    public async Task<IActionResult> SendMessage([FromBody] string message)
    {
        Console.WriteLine("[RABBITMQ] SendMessage endpoint blev ramt!");

        try
        {
            var hostName = Environment.GetEnvironmentVariable("RabbitMQ__HostName") ?? "rabbitmq";
            var userName = Environment.GetEnvironmentVariable("RabbitMQ__UserName") ?? "guest";
            var password = Environment.GetEnvironmentVariable("RabbitMQ__Password") ?? "guest";

            Console.WriteLine($"[RABBITMQ] Forsøger forbindelse til host: {hostName}, user: {userName}");

            var factory = new ConnectionFactory()
            {
                HostName = hostName,
                UserName = userName,
                Password = password
            };

            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(
                queue: "talon-security-queue",
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var body = Encoding.UTF8.GetBytes(message ?? "Tom besked");

            await channel.BasicPublishAsync(
                exchange: "",
                routingKey: "talon-security-queue",
                body: body);

            Console.WriteLine("[RABBITMQ] Besked sendt succesfuldt!");
            return Ok(new { Success = true, Message = $"Besked sendt: {message}" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RABBITMQ ERROR] {ex.GetType().Name}: {ex.Message}");
            return StatusCode(500, new { Error = ex.Message, Type = ex.GetType().Name });
        }
    }
}