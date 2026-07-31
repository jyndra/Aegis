using System.Text.Json.Serialization;

namespace Aegis.Core.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UnlockStage
{
    Locked = 0,
    Stage1_CoolingOff = 1,
    Stage2_Passphrase = 2,
    Unlocked = 3
}

public record CommitLockStatus(
    bool Locked,
    UnlockStage Stage,
    DateTimeOffset StageChangedAt,
    DateTimeOffset LockExpiresAt,
    bool CanAdvance,
    long SecondsRemainingInStage,
    DateTimeOffset? NextStageAvailableAt
);
