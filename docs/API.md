# Aegis Local API

## 1. Overview

The local API is the communication layer between the UI, browser extension, watchdog, and service. It must be authenticated, local-only, and minimal.

## 2. Transport

Preferred transport:
- localhost HTTPS with self-signed local trust plus token auth, or
- named pipes on Windows for privileged actions, with a thin HTTP bridge for extension compatibility.

## 3. Authentication protocol

### 3.1 Overview

All API calls (except `GET /health` with limited response) require a valid session token. Tokens are HMAC-SHA256 signed JWTs issued by the service.

### 3.2 Component identity

Each component is assigned an identity during installation:

| Component | Identity type | Issued how |
|---|---|---|
| UI | `aegis-ui` | Pre-shared component ID embedded in the UI binary config |
| Browser Extension | `aegis-extension-{browser}` | Pre-shared secret written to extension storage during install |
| Watchdog | `aegis-watchdog` | Named pipe authentication (Windows impersonation of service account) |

### 3.3 Handshake flow (extension and UI)

1. Client sends `POST /handshake` with: `{ componentId, timestamp, hmac(componentId + timestamp, preSharedSecret) }`.
2. Service verifies the HMAC using the stored pre-shared secret for that component.
3. Service verifies the timestamp is within ±30 seconds of server time.
4. Service issues a session token (JWT): `{ sub: componentId, iat, exp (5 minutes), nonce }`.
5. Token is signed with the API session secret (HMAC-SHA256).
6. Client includes the token in all subsequent requests as `Authorization: Bearer {token}`.

### 3.4 Handshake flow (watchdog)

The watchdog communicates via named pipes. Windows named pipe impersonation verifies the caller is running as `LocalSystem`. No token exchange needed — the pipe ACL is the auth mechanism.

### 3.5 Token properties

- **Expiration:** 5 minutes. Clients must re-handshake before expiry.
- **Nonce:** Included in the token. Each token has a unique nonce tracked by the service. Rejected if reused (anti-replay).
- **Scope:** Tokens are scoped to the component ID. The extension token cannot call UI-only endpoints and vice versa.

### 3.6 Rate limiting

| Endpoint | Limit |
|---|---|
| `POST /handshake` | 10 per minute per component |
| `POST /unlock/request` | 3 per day (configurable) |
| `POST /unlock/advance` | 1 per cooldown period |
| `POST /evaluate` | 1000 per minute (burst) |
| All other POST endpoints | 60 per minute per component |

### 3.7 Anti-tampering

- Tokens are validated on every request (signature + expiration + nonce).
- Invalid tokens return `AEGIS-2003` and the attempt is logged.
- Repeated invalid token submissions (>10 in 1 minute) trigger a temporary lockout of the source component ID.

## 4. Core endpoints

### GET /health
Returns service health, module status, lock state, and degraded state.

### GET /policy
Returns the active policy pack and current enforcement rules.

### POST /handshake
Used by the browser extension to authenticate and receive a short-lived session token.

### POST /event
Records a local event from a trusted module.

### POST /evaluate
Submits a URL/query/title/metadata payload for decisioning.

### POST /unlock/request
Begins the unlock request workflow.

### POST /unlock/advance
Advances one unlock stage after the cooldown and challenge are completed.

### POST /unlock/cancel
Cancels the current unlock attempt and resets progress.

### POST /install/verify
Verifies installer-created components are present.

### POST /integrity/check
Triggers a full integrity audit.

### POST /repair
Attempts self-healing for a specific component.

### GET /status/report
Returns a detailed protection report for the UI.

### Custom Policy & Rule Provisions

#### GET /policy/custom-rules
Returns custom rules overview: list of custom blocked websites, keywords, regex rules, commitment lock status, and `testModeActive` flag.

#### POST /policy/custom-websites
Body: `{ "domain": "example.com" }`. Adds custom domain to SQLite `domain_blocklist` and triggers hot-reloading in `DnsFilter`. Always allowed even during 25-day lock.

#### POST /policy/custom-keywords
Body: `{ "keyword": "gambling", "weight": 50 }`. Adds custom keyword rule to SQLite `blocked_rules` and triggers hot-reloading in `KeywordEngine`. Always allowed even during 25-day lock.

#### POST /policy/custom-regex
Body: `{ "pattern": "\\b(poker|bet)\\b", "score": 60, "description": "Custom regex" }`. Validates regex syntax and adds to `blocked_rules`, triggering `RegexEngine` hot-reload. Always allowed even during 25-day lock.

#### DELETE /policy/custom-rules/{id}
Deletes custom rule. **Enforces One-Way Protection Ratchet**: blocked (HTTP 403) while 25-day commitment lock is active in production mode. Permitted in test mode (`bypassLockForTesting: true`).

### Deployment & Uninstallation Endpoints

#### POST /deployment/uninstall
Accepts `forceConfirm: bool` and `step: int`.
- **Test Mode (`bypassLockForTesting: true`)**: Executes instant 1-click zero-friction uninstallation.
- **Production Mode (`bypassLockForTesting: false`)**: Blocked if 25-day commitment lock is live. If unlocked, enforces **10-Step Interactive Challenge with 5-Minute Cooldown Per Step** (requiring `step=1..10` with 5 minutes wait between each step; 50 minutes total cumulative delay).

## 5. Payload model

Inputs may include:
- url,
- domain,
- path,
- query,
- title,
- referrer,
- browser,
- process,
- component,
- timestamp,
- rule context.

## 6. Response model

Responses should return:
- decision,
- reason,
- severity,
- action,
- component state,
- next step,
- retry-after where relevant.

## 7. Error handling

All errors must be explicit and machine-readable:
- unauthorized,
- invalid token,
- component unavailable,
- rule engine failure,
- lock active,
- degraded state,
- repair pending,
- rate limited.

## 8. Versioning

Version all endpoints and support backward-compatible evolution where possible.

## 9. Extension protocol

The browser extension should use a short-lived authenticated handshake with the local API. The extension must never assume the service is trustworthy without verification.

## 10. Security considerations

- localhost only,
- no public listening ports,
- no anonymous state-changing requests,
- all policy changes must be signed or otherwise authenticated.
