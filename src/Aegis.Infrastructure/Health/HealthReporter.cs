using Aegis.Core.Interfaces;
using Aegis.Core.Models;

namespace Aegis.Infrastructure.Health;

internal class HealthReporter : IHealthReporter
{
    public Task RecordHealthAsync(string component, string status, string detailsJson, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<HealthReport>> GetStatusReportAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
