using Aegis.Core.Models;

namespace Aegis.Core.Interfaces;

/// <summary>
/// Enforces commitment lock duration, monotonic timer checks, and multi-stage unlock workflow.
/// </summary>
public interface ICommitLockEngine
{
    Task<LockState> GetLockStateAsync(CancellationToken cancellationToken = default);
    Task<bool> RequestUnlockAsync(CancellationToken cancellationToken = default);
    Task<LockState> AdvanceUnlockStageAsync(int currentStage, CancellationToken cancellationToken = default);
    Task CancelUnlockAsync(CancellationToken cancellationToken = default);
    Task ValidateClockIntegrityAsync(CancellationToken cancellationToken = default);
}
