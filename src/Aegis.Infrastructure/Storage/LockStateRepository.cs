using Aegis.Core.Interfaces;
using Aegis.Core.Models;

namespace Aegis.Infrastructure.Storage;

internal class LockStateRepository : ILockStateRepository
{
    public Task<LockState?> GetCurrentLockStateAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task SaveLockStateAsync(LockState state, CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
