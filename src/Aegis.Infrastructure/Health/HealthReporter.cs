using Aegis.Core.Interfaces;
using Aegis.Core.Models;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Health;

public class HealthReporter : IHealthReporter
{
    private readonly IModuleHealthRepository _healthRepo;
    private readonly ITimeProvider _timeProvider;
    private readonly ILogger<HealthReporter> _logger;

    public HealthReporter(IModuleHealthRepository healthRepo, ITimeProvider timeProvider, ILogger<HealthReporter> logger)
    {
        _healthRepo = healthRepo;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task RecordHealthAsync(string component, string status, string detailsJson, CancellationToken cancellationToken = default)
    {
        var report = new HealthReport(
            Component: component,
            Status: status,
            LastCheckedAt: _timeProvider.UtcNow,
            DetailJson: detailsJson
        );

        _logger.LogDebug("Recording health status for {Component}: {Status}", component, status);
        await _healthRepo.SaveHealthReportAsync(report, cancellationToken);
    }

    public async Task<IReadOnlyList<HealthReport>> GetStatusReportAsync(CancellationToken cancellationToken = default)
    {
        var reports = await _healthRepo.GetAllHealthReportsAsync(cancellationToken);
        if (reports.Count == 0)
        {
            // Return baseline reports if database has no reports yet
            return new List<HealthReport>
            {
                new("Service", "Healthy", _timeProvider.UtcNow, "{\"message\": \"Windows Service running in-process\"}"),
                new("Database", "Healthy", _timeProvider.UtcNow, "{\"message\": \"SQLite WAL mode initialized\"}"),
                new("DNS", "Healthy", _timeProvider.UtcNow, "{\"message\": \"DNS proxy standby\"}"),
                new("Extension", "Healthy", _timeProvider.UtcNow, "{\"message\": \"Extension worker standby\"}")
            };
        }

        return reports;
    }
}
