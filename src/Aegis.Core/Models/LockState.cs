namespace Aegis.Core.Models;

/// <summary>
/// Domain model representing the commitment lock state.
/// </summary>
public record LockState(
    long Id,
    bool IsLocked,
    DateTimeOffset ActivatedAt,
    DateTimeOffset ExpiresAt,
    long ActivatedMonotonicTicks,
    long ElapsedMonotonicTicks,
    DateTimeOffset LastTickUpdateAt,
    DateTimeOffset? UnlockRequestedAt,
    int UnlockStage,
    string UnlockState,
    DateTimeOffset LastChangeAt,
    string RowHmac
);
