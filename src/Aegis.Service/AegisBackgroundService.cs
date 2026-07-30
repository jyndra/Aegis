using Aegis.Core.Configuration;
using Aegis.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace Aegis.Service;

public class AegisBackgroundService : BackgroundService
{
    private readonly IStorageService _storageService;
    private readonly IDnsFilter _dnsFilter;
    private readonly IProxyServer _proxyServer;
    private readonly IHealthReporter _healthReporter;
    private readonly IIntegrityEngine _integrityEngine;
    private readonly ITimeProvider _timeProvider;
    private readonly IOptions<ServiceOptions> _options;
    private readonly ILogger<AegisBackgroundService> _logger;
    private DateTimeOffset _startTime;

    public AegisBackgroundService(
        IStorageService storageService,
        IDnsFilter dnsFilter,
        IProxyServer proxyServer,
        IHealthReporter healthReporter,
        IIntegrityEngine integrityEngine,
        ITimeProvider timeProvider,
        IOptions<ServiceOptions> options,
        ILogger<AegisBackgroundService> logger)
    {
        _storageService = storageService;
        _dnsFilter = dnsFilter;
        _proxyServer = proxyServer;
        _healthReporter = healthReporter;
        _integrityEngine = integrityEngine;
        _timeProvider = timeProvider;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _startTime = _timeProvider.UtcNow;
        _logger.LogInformation("Aegis Protection Service starting...");

        try
        {
            // 1. Initialize SQLite Database & apply migrations
            _logger.LogInformation("Initializing local storage and schema migrations...");
            await _storageService.InitializeDatabaseAsync(stoppingToken);

            bool dbIntegrity = await _storageService.CheckIntegrityAsync(stoppingToken);
            _logger.LogInformation("Database integrity check result: {Status}", dbIntegrity ? "OK" : "FAILED");

            // 2. Start DNS Filtering proxy listener
            _logger.LogInformation("Starting DNS Filtering Engine...");
            await _dnsFilter.StartAsync(stoppingToken);

            // 3. Start Local HTTP Proxy Server (Isolated dev port 19080)
            _logger.LogInformation("Starting Aegis HTTP Proxy Server...");
            await _proxyServer.StartAsync(stoppingToken);

            // 4. Record initial health status
            await _healthReporter.RecordHealthAsync("Service", "Healthy", "{\"message\":\"Windows Service running\"}", stoppingToken);
            await _healthReporter.RecordHealthAsync("Database", dbIntegrity ? "Healthy" : "Degraded", "{\"message\":\"SQLite WAL mode initialized\"}", stoppingToken);
            await _healthReporter.RecordHealthAsync("DNS", "Healthy", "{\"message\":\"DNS proxy active\"}", stoppingToken);
            await _healthReporter.RecordHealthAsync("Extension", "Healthy", "{\"message\":\"Browser extension worker standby\"}", stoppingToken);

            // 5. Run Boot-Time Integrity Audit
            _logger.LogInformation("Running Boot-Time Integrity Audit...");
            var bootAudit = await _integrityEngine.RunBootAuditAsync(stoppingToken);
            _logger.LogInformation("Boot-Time Integrity Audit complete. Status: {Status}", bootAudit.Healthy ? "PASSED" : "DEGRADED/HEALED");

            _logger.LogInformation("Aegis Protection Service successfully initialized and running.");
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

                // Periodic health pulse & integrity audit
                bool dbOk = await _storageService.CheckIntegrityAsync(stoppingToken);
                long uptimeSec = (long)(_timeProvider.UtcNow - _startTime).TotalSeconds;

                await _healthReporter.RecordHealthAsync("Service", "Healthy", $"{{\"uptimeSec\": {uptimeSec}}}", stoppingToken);
                await _healthReporter.RecordHealthAsync("Database", dbOk ? "Healthy" : "Degraded", $"{{\"integrity\": \"{(dbOk ? "OK" : "FAILED")}\"}}", stoppingToken);

                // Periodic Integrity Check
                await _integrityEngine.RunPeriodicAuditAsync(stoppingToken);

                _logger.LogDebug("Periodic Aegis health check & integrity pulse executed.");
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

        // Clean shutdown
        await _proxyServer.StopAsync(CancellationToken.None);
        await _dnsFilter.StopAsync(CancellationToken.None);
        _logger.LogInformation("Aegis Protection Service background loops shutting down gracefully.");
    }
}
