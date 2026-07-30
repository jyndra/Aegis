namespace Aegis.Core.Configuration;

public class LockOptions
{
    public const string SectionName = "lock";

    public int DefaultLockDays { get; set; } = 25;
    public int UnlockCooldownMinutes { get; set; } = 60;
    public int UnlockStages { get; set; } = 3;
    public int MaxUnlockAttemptsPerDay { get; set; } = 3;
}
