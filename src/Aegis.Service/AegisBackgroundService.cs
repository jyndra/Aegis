using Aegis.Core.Configuration;
using Aegis.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace Aegis.Service;

public class AegisBackgroundService : BackgroundService
{
    private readonly IStorageService _storageService;
    private readonly IHealthReporter _healthReporter;
    private readonly ITimeProvider _timeProvider;
    private readonly IOptions<ServiceOptions> _options;
    private readonly ILogger<AegisBackgroundService> _logger;
    private DateTimeOffset _startTime;

    public AegisBackgroundService(
        IStorageService storageService,
        IHealthReporter healthReporter,
        ITimeProvider timeProvider,
        IOptions<ServiceOptions> options,
        ILogger<AegisBackgroundService> logger)
    {
        _storageService = storageService;
        _healthReporter = healthReporter;
        _timeProvider = timeProvider;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _startTime = _timeProvider.UtcNow;
        _logger.LogInformation("Aegis Protection Service starting (Milestone 1)...");

        try
        {
            // 1. Initialize SQLite Database & apply migrations
            _logger.LogInformation("Initializing local storage and schema migrations...");
            await _storageService.InitializeDatabaseAsync(stoppingToken);

            bool dbIntegrity = await _storageService.CheckIntegrityAsync(stoppingToken);
            _logger.LogInformation("Database integrity check result: {Status}", dbIntegrity ? "OK" : "FAILED");

            // 2. Record initial health status
            await _healthReporter.RecordHealthAsync("Service", "Healthy", "{\"message\":\"Windows Service running\"}", stoppingToken);
            await _healthReporter.RecordHealthAsync("Database", dbIntegrity ? "Healthy" : "Degraded", "{\"message\":\"SQLite WAL mode initialized\"}", stoppingToken);
            await _healthReporter.RecordHealthAsync("DNS", "Healthy", "{\"message\":\"DNS proxy standby\"}", stoppingToken);
            await _healthReporter.RecordHealthAsync("Extension", "Healthy", "{\"message\":\"Browser extension worker standby\"}", stoppingToken);

            _logger.LogInformation("Aegis Protection Service successfully initialized. Milestone 1 active.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error during Aegis Service startup!");
            await _healthReporter.RecordHealthAsync("Service", "Degraded", $"{{\"error\":\"{ex.Message}\"}}", stoppingToken);
        }

        int intervalSeconds = Math.Max(5, _options.Value.HealthCheckIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);

                // Periodic health pulse
                bool dbOk = await _storageService.CheckIntegrityAsync(stoppingToken);
                long uptimeSec = (long)(_timeProvider.UtcNow - _startTime).TotalSeconds;

                await _healthReporter.RecordHealthAsync("Service", "Healthy", $"{{\"uptimeSec\": {uptimeSec}}}", stoppingToken);
                await _healthReporter.RecordHealthAsync("Database", dbOk ? "Healthy" : "Degraded", $"{{\"integrity\": \"{(dbOk ? "OK" : "FAILED")}\"}}", stoppingToken);

                _logger.LogDebug("Periodic Aegis health check pulse executed.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Aegis periodic health check loop.");
            }
        }

        _logger.LogInformation("Aegis Protection Service background loops shutting down gracefully.");
    }
}
