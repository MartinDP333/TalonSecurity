using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Worker;

public class TalonBackgroundWorker : BackgroundService
{
    private readonly ILogger<TalonBackgroundWorker> _logger;

    public TalonBackgroundWorker(ILogger<TalonBackgroundWorker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Talon Security Worker er startet og lytter i baggrunden...");

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker tjekker systemstatus kl: {time}", DateTimeOffset.Now);

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}