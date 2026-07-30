using Aegis.Core.Models;

namespace Aegis.Core.Interfaces;

public interface IModuleHealthRepository
{
    Task SaveHealthReportAsync(HealthReport report, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HealthReport>> GetAllHealthReportsAsync(CancellationToken cancellationToken = default);
}
