# Aegis Recovery Playbook

## 1. Purpose

Define concrete recovery procedures for each failure scenario. Every scenario specifies what the system does automatically and what the user should do manually if automatic recovery fails.

## 2. Degraded mode behaviors

When the system enters degraded mode, it does **one or more** of the following depending on configuration:

| Behavior | Default | Configurable? |
|---|---|---|
| Block all web traffic via DNS (return `0.0.0.0` for all queries) | Yes | Yes |
| Display a local status page explaining the degraded state | Yes | No |
| Show a persistent system tray notification | Yes | No |
| Log the degraded state and reason | Yes | No |
| Continue attempting self-healing at intervals | Yes | Yes (interval) |

Degraded mode is **not** a permanent state. The system continuously attempts to restore health and exits degraded mode automatically when all critical components are verified.

## 3. Failure scenarios

### 3.1 Service fails to start

**Detection:** Windows SCM reports the service did not start. Watchdog detects the service is not running.

**Automatic recovery:**
1. SCM retries the service start (configured for 3 retries with 30-second intervals).
2. If SCM retries fail, the watchdog attempts a start.
3. If the watchdog also fails, it logs `AEGIS-1001` and enters degraded mode.

**User action:** Check the Windows Event Log and Aegis logs in `%ProgramData%\Aegis\logs\` for the startup error. Common causes: port conflict, database corruption, missing config.

### 3.2 Service crashes during runtime

**Detection:** Watchdog heartbeat fails (no API response within 10 seconds).

**Automatic recovery:**
1. Watchdog waits 5 seconds and retries the health check.
2. If still unresponsive, watchdog restarts the service.
3. If restart succeeds, the service re-validates integrity on startup.
4. If restart fails 3 times within 5 minutes (restart storm), the watchdog stops retrying and enters degraded mode. Logs `AEGIS-1003`.

**User action:** Check crash logs. If the service is in a restart loop, reinstall or repair the Aegis installation.

### 3.3 Browser extension missing

**Detection:** The integrity engine does not receive an extension heartbeat within 2 heartbeat intervals (default: 2 minutes).

**Automatic recovery:**
1. Log `AEGIS-6002`.
2. Check if the extension registry policy is still present.
3. If the policy is present, the extension may have been disabled by the user — log the event and notify via tray icon.
4. If the policy is missing, attempt to re-apply it (requires admin elevation).
5. If re-application fails, enter degraded mode.

**User action:** Re-enable the extension in the browser. If the extension was uninstalled and policies were removed, run the Aegis repair tool from the system tray or reinstall.

### 3.4 DNS configuration changed externally

**Detection:** The integrity engine checks the system's primary DNS setting every 60 seconds (configurable). If it does not point to `127.0.0.1`, a drift is detected.

**Automatic recovery:**
1. Log `AEGIS-4004`.
2. Attempt to reset the primary DNS to `127.0.0.1` on all active non-VPN network adapters. This requires elevation.
3. If successful, log the restoration.
4. If elevation is unavailable or the reset fails, enter degraded mode. Log `AEGIS-4005`.

**User action:** Do not manually change DNS settings while Aegis is active. If DNS was changed by a VPN, the browser extension layer continues to provide protection; the DNS drift is logged but the extension layer is the primary defense.

### 3.5 Database file deleted or corrupted

**Detection:** On startup and periodically, the service runs `PRAGMA integrity_check` and validates HMAC on critical rows.

**Automatic recovery:**
1. If corruption is detected, log `AEGIS-8001`.
2. Attempt to restore from the latest backup (`%ProgramData%\Aegis\backups\aegis.db.bak`).
3. Verify the backup's integrity check and HMAC.
4. If the backup is valid, replace the active database and restart.
5. If the backup is also corrupted or missing, log `AEGIS-8004` and enter degraded mode with hardcoded defaults. The lock timer defaults to "locked" (fail closed).

**User action:** If the database and backup are both lost, the system operates in degraded mode. The user must reinstall Aegis to create a fresh database. Lock state is non-recoverable by design (prevents bypass by database deletion).

### 3.6 Database file tampered (HMAC mismatch)

**Detection:** HMAC validation on `lock_state` or `policy_versions` rows fails.

**Automatic recovery:**
1. Log `AEGIS-8006` and `AEGIS-7004`.
2. Do **not** trust the tampered data.
3. Attempt restore from backup (same as 3.5).
4. If backup is valid, restore and continue.
5. If backup also fails validation, enter degraded mode with lock defaulting to "locked."

**User action:** Same as 3.5. The tamper event is permanently logged in the event history.

### 3.7 Config file tampered

**Detection:** HMAC on `appsettings.json` does not match `appsettings.json.sig`.

**Automatic recovery:**
1. Log `AEGIS-9004`.
2. Ignore the tampered config.
3. Fall back to hardcoded safe defaults.
4. Enter degraded mode if critical settings cannot be determined.

**User action:** Use the Aegis UI or API to reconfigure settings. The service will re-sign the config file on the next valid save.

### 3.8 Lock timer clock manipulation

**Detection:** The service compares wall-clock elapsed time against monotonic elapsed time (`QueryUnbiasedInterruptTime` or `Stopwatch.GetElapsedTime` since boot). If the wall-clock time has jumped forward or backward by more than 5 minutes relative to monotonic time (allowing for sleep/hibernate), a clock manipulation is suspected.

**Automatic recovery:**
1. Log `AEGIS-3006`.
2. **Do not advance the lock timer.** The lock timer relies on monotonic ticks stored in the database, not wall-clock time.
3. Optionally cross-reference with NTP (if network is available) to confirm the actual time.
4. If NTP confirms the system clock is wrong, log the discrepancy but take no action beyond logging.
5. The lock expiration is computed as: `lock_start_monotonic_ticks + lock_duration_ticks`, verified against both monotonic time and persisted tick counts.

**User action:** None needed. The lock timer is resilient to clock changes by design.

### 3.9 Watchdog itself is killed

**Detection:** The watchdog runs as a separate Windows Service. If it is killed, there is no further monitoring of the main service.

**Automatic recovery:**
1. The watchdog service has its own SCM recovery policy (restart on failure, 3 retries).
2. The main service periodically checks if the watchdog is alive and logs if it is missing.
3. If both the main service and watchdog are killed, degraded mode depends on the next boot (both are set to auto-start).

**User action:** If the watchdog is persistently killed, the machine is under active administrative tampering — outside Aegis's threat model.

### 3.10 Service restart storm

**Detection:** The watchdog counts restarts within a rolling 5-minute window. If more than 3 restarts occur, a restart storm is declared.

**Automatic recovery:**
1. Stop attempting restarts.
2. Log `AEGIS-1003` with storm context.
3. Enter degraded mode.
4. Wait for the next system boot to retry.

**User action:** Check logs for the root cause of repeated crashes. Fix the underlying issue and reboot.

## 4. Recovery priority matrix

| Scenario | Severity | Auto-recovery? | Fail-closed? |
|---|---|---|---|
| Service won't start | Critical | Yes (SCM + watchdog) | Yes |
| Service crash | Critical | Yes (watchdog) | Yes |
| Extension missing | High | Partial (policy re-apply) | Yes |
| DNS drift | High | Yes (reset DNS) | Yes |
| Database deleted | Critical | Yes (backup restore) | Yes (lock defaults to locked) |
| Database tampered | Critical | Yes (backup restore) | Yes (lock defaults to locked) |
| Config tampered | Medium | Yes (fall back to defaults) | Yes |
| Clock manipulation | Medium | N/A (inherently resistant) | Yes |
| Watchdog killed | Medium | Yes (SCM restart) | No (depends on main service) |
| Restart storm | Critical | No (stop retrying) | Yes |

## 5. Principles

1. **Fail closed.** When in doubt, block. Never silently disable protection.
2. **Lock defaults to locked.** If lock state is unknown, assume locked.
3. **Log everything.** Every recovery action and failure is recorded.
4. **No data destruction.** Recovery never deletes user data, browser data, or unrelated system files.
5. **Automatic where possible.** The user should rarely need to intervene manually.
