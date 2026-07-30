using Aegis.Core.Interfaces;
using Aegis.Core.Models;

namespace Aegis.Infrastructure.Storage;

internal class ModuleHealthRepository : IModuleHealthRepository
{
    public Task SaveHealthReportAsync(HealthReport report, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<HealthReport>> GetAllHealthReportsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
