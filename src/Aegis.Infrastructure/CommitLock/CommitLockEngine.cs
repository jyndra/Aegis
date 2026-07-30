using Aegis.Core.Interfaces;
using Aegis.Core.Models;

namespace Aegis.Infrastructure.CommitLock;

internal class CommitLockEngine : ICommitLockEngine
{
    public Task<LockState> GetLockStateAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<bool> RequestUnlockAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<LockState> AdvanceUnlockStageAsync(int currentStage, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task CancelUnlockAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task ValidateClockIntegrityAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
