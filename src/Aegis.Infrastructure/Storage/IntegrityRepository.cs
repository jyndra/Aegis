using Aegis.Core.Interfaces;
using Aegis.Core.Models;

namespace Aegis.Infrastructure.Storage;

internal class IntegrityRepository : IIntegrityRepository
{
    public Task AddIntegrityResultAsync(IntegrityResult result, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<IntegrityResult>> GetRecentIntegrityResultsAsync(int count = 20, CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
