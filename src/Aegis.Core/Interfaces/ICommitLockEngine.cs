using Aegis.Core.Models;

namespace Aegis.Core.Interfaces;

public interface ICommitLockEngine
{
    Task<LockState> GetLockStateAsync(CancellationToken cancellationToken = default);
    Task<CommitLockStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<LockState> LockAsync(int durationDays = 25, CancellationToken cancellationToken = default);
    Task<UnlockProgress> InitiateUnlockStageAsync(int targetStage, string? confirmationChallenge = null, CancellationToken cancellationToken = default);
    Task<bool> IsUnlockAllowedAsync(CancellationToken cancellationToken = default);
    Task ResetFailedAttemptsAsync(CancellationToken cancellationToken = default);
}
