namespace Aegis.Core.Errors;

/// <summary>
/// System-wide error code constants matching ERRORS.md taxonomy.
/// </summary>
public static class AegisErrorCodes
{
    // Service and hosting (1xxx)
    public const string ServiceStartFailed = "AEGIS-1001";
    public const string ServiceUnreachable = "AEGIS-1002";
    public const string WatchdogRestartFailed = "AEGIS-1003";
    public const string HostBindFailed = "AEGIS-1004";
    public const string PortInUse = "AEGIS-1004";
    public const string DegradedModeEntered = "AEGIS-1005";
    public const string DegradedModeActivated = "AEGIS-1005";
    public const string DegradedModeExited = "AEGIS-1006";
    public const string ShutdownRequested = "AEGIS-1007";

    // Authentication and authorization (2xxx)
    public const string Unauthorized = "AEGIS-2001";
    public const string TokenExpired = "AEGIS-2002";
    public const string TokenInvalid = "AEGIS-2003";
    public const string NonceReused = "AEGIS-2004";
    public const string ComponentIdentityInvalid = "AEGIS-2005";
    public const string RateLimited = "AEGIS-2006";
    public const string PermissionDenied = "AEGIS-2007";

    // Commitment lock (3xxx)
    public const string LockActive = "AEGIS-3001";
    public const string UnlockCooldownActive = "AEGIS-3002";
    public const string UnlockMaxAttemptsExceeded = "AEGIS-3003";
    public const string UnlockStageInvalid = "AEGIS-3004";
    public const string UnlockExpired = "AEGIS-3005";
    public const string ClockManipulationDetected = "AEGIS-3006";

    // DNS module (4xxx)
    public const string DnsBindFailed = "AEGIS-4001";
    public const string DnsUpstreamUnreachable = "AEGIS-4002";
    public const string DnsBlocklistLoadFailed = "AEGIS-4003";
    public const string DnsConfigDrift = "AEGIS-4004";
    public const string DnsRestoreFailed = "AEGIS-4005";

    // Rule engine (5xxx)
    public const string RuleEvaluationTimeout = "AEGIS-5001";
    public const string RegexCompilationFailed = "AEGIS-5002";
    public const string KeywordPackLoadFailed = "AEGIS-5003";
    public const string PolicyPackInvalid = "AEGIS-5004";
    public const string PolicyPackMissing = "AEGIS-5005";

    // Browser extension (6xxx)
    public const string ExtensionHandshakeFailed = "AEGIS-6001";
    public const string ExtensionMissing = "AEGIS-6002";
    public const string ExtensionHealthTimeout = "AEGIS-6003";
    public const string ExtensionPolicyStale = "AEGIS-6004";

    // Integrity (7xxx)
    public const string IntegrityCheckFailed = "AEGIS-7001";
    public const string StateIntegrityViolated = "AEGIS-7001";
    public const string SelfHealingAttempted = "AEGIS-7002";
    public const string SelfHealingFailed = "AEGIS-7003";
    public const string TamperDetected = "AEGIS-7004";
    public const string ComponentMissing = "AEGIS-7005";
    public const string FileChecksumMismatch = "AEGIS-7006";

    // Database and storage (8xxx)
    public const string DatabaseCorrupted = "AEGIS-8001";
    public const string DatabaseMissing = "AEGIS-8002";
    public const string DatabaseBackupFailed = "AEGIS-8003";
    public const string DatabaseRestoreFailed = "AEGIS-8004";
    public const string MigrationFailed = "AEGIS-8005";
    public const string HmacValidationFailed = "AEGIS-8006";
    public const string WriteFailed = "AEGIS-8007";

    // Installer and config (9xxx)
    public const string InstallFailed = "AEGIS-9001";
    public const string UninstallBlocked = "AEGIS-9002";
    public const string ConfigInvalid = "AEGIS-9003";
    public const string ConfigTampered = "AEGIS-9004";
    public const string RollbackFailed = "AEGIS-9005";
    public const string ElevationRequired = "AEGIS-9006";
    public const string ServiceRegistrationFailed = "AEGIS-9007";
}
