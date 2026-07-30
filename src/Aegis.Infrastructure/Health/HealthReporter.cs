using Aegis.Core.Interfaces;
using Aegis.Core.Models;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Health;

public class HealthReporter : IHealthReporter
{
    private readonly IModuleHealthRepository _healthRepo;
    private readonly ITimeProvider _timeProvider;
    private readonly ILogger<HealthReporter> _logger;

    private static readonly string[] RecognizedSubsystems = new[]
    {
        "Service", "Database", "DNS", "Extension", "Watchdog"
    };

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
        var dbReports = await _healthRepo.GetAllHealthReportsAsync(cancellationToken);
        var reportMap = dbReports.ToDictionary(r => r.Component, StringComparer.OrdinalIgnoreCase);

        var result = new List<HealthReport>();

        foreach (var subsystem in RecognizedSubsystems)
        {
            if (reportMap.TryGetValue(subsystem, out var existing))
            {
                result.Add(existing);
            }
            else
            {
                // Fallback baseline report if not yet recorded in database
                result.Add(new HealthReport(
                    Component: subsystem,
                    Status: "Healthy",
                    LastCheckedAt: _timeProvider.UtcNow,
                    DetailJson: $"{{\"message\": \"{subsystem} subsystem standby\"}}"
                ));
            }
        }

        // Append any custom component reports not in standard RecognizedSubsystems list
        foreach (var report in dbReports)
        {
            if (!RecognizedSubsystems.Contains(report.Component, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(report);
            }
        }

        return result;
    }
}
