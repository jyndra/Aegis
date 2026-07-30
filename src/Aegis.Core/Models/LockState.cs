namespace Aegis.Core.Models;

public record LockState(
    bool Locked,
    DateTimeOffset LockStartedAt,
    DateTimeOffset LockExpiresAt,
    DateTimeOffset? UnlockRequestedAt,
    int UnlockStage,
    int FailedAttempts,
    DateTimeOffset UpdatedAt
);

public record UnlockProgress(
    bool Success,
    int CurrentStage,
    string Message,
    TimeSpan? CooldownRemaining
);
