using Aegis.Core.Models;

namespace Aegis.Core.Interfaces;

public interface IHealthReporter
{
    Task RecordHealthAsync(string component, string status, string detailsJson, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HealthReport>> GetStatusReportAsync(CancellationToken cancellationToken = default);
}
