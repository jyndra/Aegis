# Aegis Security Guidelines

## 1. Security posture

Aegis is a local enforcement system. It should be secure by default, transparent, and auditable.

## 2. Principles

- least privilege,
- local-only by default,
- authenticated IPC,
- no anonymous state changes,
- fail closed,
- explicit recovery,
- minimal secrets exposure.

## 3. Secrets and key management

### 3.1 Key storage

Store all cryptographic keys in `%ProgramData%\Aegis\keys\`, ACL'd to the service account only (no user read/write).

### 3.2 DPAPI encryption

Use Windows DPAPI (`System.Security.Cryptography.ProtectedData`) to encrypt key material at rest. DPAPI ties the encryption to the service account's SID, meaning:
- the key cannot be decrypted by the standard user,
- the key cannot be decrypted on a different machine,
- the key survives reboots but not OS reinstalls.

### 3.3 Key types

| Key | Purpose | Storage | Rotation |
|---|---|---|---|
| HMAC key | Signing database rows (lock_state, policy) and config files | DPAPI-encrypted file | On reinstall only |
| API session secret | Deriving short-lived API tokens | DPAPI-encrypted file | On service restart |
| Extension handshake secret | HMAC-based handshake with the browser extension | DPAPI-encrypted file, shared during install | On reinstall or extension re-deploy |
| Policy signature key | Verifying policy pack integrity | DPAPI-encrypted file | On policy pack update |

### 3.4 Limitations

Local-only signing protects against accidental corruption and raises the difficulty of casual tampering, but it **cannot** prevent a determined administrator who has access to the service account (e.g., via `psexec -s`). This is an accepted limitation per the threat model (§3.3 Deliberate administrative bypass is out of scope).

## 4. Local API security

- bind only to localhost or named pipes,
- require authentication on every sensitive endpoint,
- use short-lived tokens,
- rotate session credentials,
- validate component identity.

## 5. File integrity

Protected files should have:
- checksums,
- signatures,
- version metadata,
- controlled update paths.

## 6. Policy integrity

Policy packs must be:
- versioned,
- signed or checksum-verified,
- validated on load,
- rejected if malformed.

## 7. Tamper response

When tampering is detected:
- log it,
- verify impact,
- attempt recovery if safe,
- otherwise enter degraded mode.

## 8. Safe recovery

Recovery must not:
- delete user data,
- break Windows,
- silently disable protection,
- overwrite unrelated files.

## 9. Logging privacy

Logs should:
- stay local,
- avoid unnecessary sensitive content,
- be rotated and bounded.

## 10. Testing

Test at least:
- startup failure,
- extension missing,
- DNS drift,
- service stop,
- database corruption,
- invalid token access,
- unlock flow expiration.
