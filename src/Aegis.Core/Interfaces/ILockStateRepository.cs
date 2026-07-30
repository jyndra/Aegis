using Aegis.Core.Models;

namespace Aegis.Core.Interfaces;

public interface ILockStateRepository
{
    Task<LockState?> GetCurrentLockStateAsync(CancellationToken cancellationToken = default);
    Task SaveLockStateAsync(LockState state, CancellationToken cancellationToken = default);
}
