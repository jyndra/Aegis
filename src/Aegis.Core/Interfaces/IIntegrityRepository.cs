using Aegis.Core.Models;

namespace Aegis.Core.Interfaces;

public interface IIntegrityRepository
{
    Task AddIntegrityResultAsync(IntegrityResult result, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IntegrityResult>> GetRecentIntegrityResultsAsync(int count = 20, CancellationToken cancellationToken = default);
}
