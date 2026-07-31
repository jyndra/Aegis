using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aegis.Watchdog;

public class WatchdogBackgroundService : BackgroundService
{
    private readonly ILogger<WatchdogBackgroundService> _logger;

    public WatchdogBackgroundService(ILogger<WatchdogBackgroundService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Aegis Watchdog Service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }

        _logger.LogInformation("Aegis Watchdog Service stopping.");
    }
}
