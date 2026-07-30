using Aegis.Core.Interfaces;

namespace Aegis.Service;

public class AegisBackgroundService : BackgroundService
{
    private readonly ILogger<AegisBackgroundService> _logger;

    public AegisBackgroundService(ILogger<AegisBackgroundService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Aegis Protection Service background loops started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }

        _logger.LogInformation("Aegis Protection Service background loops stopping.");
    }
}
