# Aegis Configuration

## 1. File format

Decision: **JSON** (`appsettings.json` for service, `config.json` for user preferences).

Rationale:
- native support in `Microsoft.Extensions.Configuration`,
- human-readable and editable for debugging,
- schema-validatable.

## 2. File locations

| File | Path | Owner |
|---|---|---|
| Service config | `%ProgramData%\Aegis\appsettings.json` | Service account (SYSTEM) |
| User preferences | `%ProgramData%\Aegis\user-preferences.json` | Service account (written via API) |
| Policy packs | `%ProgramData%\Aegis\policies\` | Service account |
| Database | `%ProgramData%\Aegis\aegis.db` | Service account |
| Logs | `%ProgramData%\Aegis\logs\` | Service account |
| Extension config | Extension local storage (`chrome.storage.local`) | Extension |

All files under `%ProgramData%\Aegis\` are ACL'd to the service account. The standard user has read-only access to logs and no direct write access to any config or database file.

## 3. Configuration sections

### 3.1 Service

```json
{
  "service": {
    "apiPort": 9443,
    "apiBindAddress": "127.0.0.1",
    "healthCheckIntervalSeconds": 60,
    "integrityCheckIntervalSeconds": 300,
    "logLevel": "Information",
    "logRetentionDays": 30,
    "maxLogSizeMB": 50
  }
}
```

### 3.2 DNS

```json
{
  "dns": {
    "enabled": true,
    "listenAddress": "127.0.0.1",
    "listenPort": 53,
    "upstreamServers": ["1.1.1.1", "8.8.8.8"],
    "cacheMaxEntries": 10000,
    "cacheTTLSeconds": 300
  }
}
```

### 3.3 Filtering

```json
{
  "filtering": {
    "blocklistSources": [],
    "customBlacklistPath": "policies/custom-blacklist.txt",
    "keywordPackPaths": ["policies/keywords-default.json"],
    "regexPackPaths": ["policies/regex-default.json"],
    "ruleEvaluationTimeoutMs": 5,
    "scoreThreshold": 40
  }
}
```
*Note on thresholding*: A default `scoreThreshold` of `40` guarantees that a single custom keyword or regex match (default weight `50`) immediately exceeds the safety threshold and triggers fail-closed intervention.

### 3.4 Commitment lock

```json
{
  "lock": {
    "defaultLockDays": 25,
    "unlockCooldownMinutes": 60,
    "unlockStages": 3,
    "maxUnlockAttemptsPerDay": 3,
    "bypassLockForTesting": true
  }
}
```

### 3.5 Proxy (optional)

```json
{
  "proxy": {
    "enabled": false,
    "listenPort": 8080,
    "httpsInspection": false,
    "caInstalled": false
  }
}
```

## 4. Runtime mutability

| Setting | Mutable at runtime? | Requires restart? | Locked during commitment? |
|---|---|---|---|
| `apiPort` | No | Yes | Yes |
| `logLevel` | Yes | No | No |
| `dns.enabled` | Yes | No | Yes |
| `dns.upstreamServers` | Yes | No | Yes |
| `filtering.scoreThreshold` | Yes | No | Yes |
| `lock.defaultLockDays` | No | Yes | Yes |
| `proxy.enabled` | Yes | No | Yes |
| Blocklist sources | Yes (via API) | No | Yes |
| Custom blacklist entries | Yes (via API) | No | Yes (additions only) |

"Locked during commitment" means the setting cannot be changed while the commitment lock is active. The API must enforce this.

## 5. Validation

On startup and on every config change via API:
1. Validate JSON schema.
2. Validate value ranges (ports 1–65535, intervals > 0, etc.).
3. Verify file paths exist and are readable.
4. Reject unknown keys (strict parsing).
5. Log validation failures with specific field and reason.

If the config file is missing or unparseable, the service enters degraded mode with hardcoded safe defaults.

## 6. Config integrity

The service config file (`appsettings.json`) is HMAC-signed. The HMAC is stored in a companion file (`appsettings.json.sig`). On load:
1. Verify HMAC.
2. If invalid, log a tamper event and fall back to hardcoded safe defaults.
3. Enter degraded mode if critical settings are tampered.

## 7. Layering

Configuration is resolved in this order (later wins):
1. Hardcoded defaults (compiled into the service).
2. `appsettings.json` (on-disk service config).
3. `user-preferences.json` (user preferences, limited scope).
4. API overrides (runtime, non-persistent unless explicitly saved).

Locked settings cannot be overridden by any layer during commitment.
