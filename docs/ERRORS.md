# Aegis Error Taxonomy

## 1. Purpose

Define a system-wide error code scheme so that all modules, the API, logs, and UI use consistent error identifiers.

## 2. Code format

`AEGIS-{category}{number}`

- Category is a 1-digit group identifier.
- Number is a 3-digit sequential identifier within the group.
- Example: `AEGIS-1001`

## 3. Categories

| Category | Range | Area |
|---|---|---|
| 1 | 1000–1999 | Service and hosting |
| 2 | 2000–2999 | Authentication and authorization |
| 3 | 3000–3999 | Commitment lock and unlock |
| 4 | 4000–4999 | DNS module |
| 5 | 5000–5999 | Rule engine, keyword, and regex |
| 6 | 6000–6999 | Browser extension and handshake |
| 7 | 7000–7999 | Integrity and self-healing |
| 8 | 8000–8999 | Database and storage |
| 9 | 9000–9999 | Installer, uninstaller, and config |

## 4. Error codes

### Service and hosting (1xxx)

| Code | Name | Description |
|---|---|---|
| AEGIS-1001 | ServiceStartFailed | The Windows Service failed to start. |
| AEGIS-1002 | ServiceUnreachable | The local API did not respond to a health check. |
| AEGIS-1003 | WatchdogRestartFailed | The watchdog attempted to restart the service but failed. |
| AEGIS-1004 | HostBindFailed | The API could not bind to the configured port. |
| AEGIS-1005 | DegradedModeEntered | The system entered degraded mode. |
| AEGIS-1006 | DegradedModeExited | The system exited degraded mode after recovery. |
| AEGIS-1007 | ShutdownRequested | A graceful shutdown was requested. |

### Authentication and authorization (2xxx)

| Code | Name | Description |
|---|---|---|
| AEGIS-2001 | Unauthorized | The request lacked a valid authentication token. |
| AEGIS-2002 | TokenExpired | The session token has expired. |
| AEGIS-2003 | TokenInvalid | The token signature or format is invalid. |
| AEGIS-2004 | NonceReused | A nonce was reused (replay attack detected). |
| AEGIS-2005 | ComponentIdentityInvalid | The calling component could not be verified. |
| AEGIS-2006 | RateLimited | The caller has exceeded the rate limit. |
| AEGIS-2007 | PermissionDenied | The caller lacks permission for this operation. |

### Commitment lock (3xxx)

| Code | Name | Description |
|---|---|---|
| AEGIS-3001 | LockActive | The operation is blocked because the commitment lock is active. |
| AEGIS-3002 | UnlockCooldownActive | An unlock stage cannot be advanced until the cooldown expires. |
| AEGIS-3003 | UnlockMaxAttemptsExceeded | The daily unlock attempt limit has been reached. |
| AEGIS-3004 | UnlockStageInvalid | The unlock stage transition is invalid. |
| AEGIS-3005 | UnlockExpired | The unlock workflow expired before completion. |
| AEGIS-3006 | ClockManipulationDetected | A system clock jump was detected during lock enforcement. |

### DNS module (4xxx)

| Code | Name | Description |
|---|---|---|
| AEGIS-4001 | DnsBindFailed | The DNS module could not bind to the configured port. |
| AEGIS-4002 | DnsUpstreamUnreachable | The upstream DNS server did not respond. |
| AEGIS-4003 | DnsBlocklistLoadFailed | A blocklist file could not be loaded or parsed. |
| AEGIS-4004 | DnsConfigDrift | The system DNS configuration was changed externally. |
| AEGIS-4005 | DnsRestoreFailed | The DNS module could not restore the expected configuration. |

### Rule engine (5xxx)

| Code | Name | Description |
|---|---|---|
| AEGIS-5001 | RuleEvaluationTimeout | Rule evaluation exceeded the configured time budget. |
| AEGIS-5002 | RegexCompilationFailed | A regex pattern could not be compiled. |
| AEGIS-5003 | KeywordPackLoadFailed | A keyword pack file could not be loaded. |
| AEGIS-5004 | PolicyPackInvalid | A policy pack failed validation or signature check. |
| AEGIS-5005 | PolicyPackMissing | The expected policy pack is not present. |

### Browser extension (6xxx)

| Code | Name | Description |
|---|---|---|
| AEGIS-6001 | ExtensionHandshakeFailed | The extension could not complete its handshake with the service. |
| AEGIS-6002 | ExtensionMissing | The browser extension was not detected during an integrity check. |
| AEGIS-6003 | ExtensionHealthTimeout | The extension did not report health within the expected interval. |
| AEGIS-6004 | ExtensionPolicyStale | The extension's cached policy is outdated. |

### Integrity (7xxx)

| Code | Name | Description |
|---|---|---|
| AEGIS-7001 | IntegrityCheckFailed | A component failed its integrity check. |
| AEGIS-7002 | SelfHealingAttempted | A self-healing action was attempted. |
| AEGIS-7003 | SelfHealingFailed | A self-healing action did not resolve the issue. |
| AEGIS-7004 | TamperDetected | Tampering was detected on a protected file or config. |
| AEGIS-7005 | ComponentMissing | A required component is not present. |
| AEGIS-7006 | FileChecksumMismatch | A file's checksum does not match the expected value. |

### Database and storage (8xxx)

| Code | Name | Description |
|---|---|---|
| AEGIS-8001 | DatabaseCorrupted | The SQLite database failed an integrity check. |
| AEGIS-8002 | DatabaseMissing | The database file is not present. |
| AEGIS-8003 | DatabaseBackupFailed | A database backup could not be created. |
| AEGIS-8004 | DatabaseRestoreFailed | A database restore from backup failed. |
| AEGIS-8005 | MigrationFailed | A schema migration did not complete successfully. |
| AEGIS-8006 | HmacValidationFailed | An HMAC check on a critical database row failed. |
| AEGIS-8007 | WriteFailed | A write to the database was rejected (SQLITE_BUSY or other). |

### Installer and config (9xxx)

| Code | Name | Description |
|---|---|---|
| AEGIS-9001 | InstallFailed | The installer could not complete all steps. |
| AEGIS-9002 | UninstallBlocked | Uninstall was blocked by the commitment lock. |
| AEGIS-9003 | ConfigInvalid | A configuration file failed validation. |
| AEGIS-9004 | ConfigTampered | A configuration file's HMAC did not match. |
| AEGIS-9005 | RollbackFailed | An installation rollback could not undo all changes. |
| AEGIS-9006 | ElevationRequired | The operation requires administrator elevation. |
| AEGIS-9007 | ServiceRegistrationFailed | The Windows Service could not be registered with SCM. |

## 5. Usage guidelines

- Every error returned by the API must include the error code, human-readable name, and a descriptive message.
- Every structured log entry for an error must include the error code.
- The UI should map error codes to user-friendly descriptions (defined in UI_GUIDELINES.md tone).
- New error codes must be appended to the appropriate category — never reuse or renumber retired codes.
