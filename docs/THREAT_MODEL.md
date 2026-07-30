# Aegis Threat Model

## 1. Assets to protect

- The user's intention not to access pornographic content impulsively.
- The integrity of the blocker configuration.
- The running state of the enforcement components.
- The local keyword and domain policy databases.
- The lock timer and unlock workflow.
- The reliability of the user's web filtering environment.

## 2. Assumed environment

- Personal Windows laptop.
- User is the machine owner.
- User has administrative control in normal operating conditions.
- Browser usage occurs across Chromium-based browsers.
- VPNs may be installed and used.
- Internet access may need to be blocked or reduced when integrity fails.

## 3. Adversaries

### 3.1 Impulsive user
The main adversary is future-me during a craving. This adversary:
- wants the easiest bypass,
- is time-limited,
- is likely to use normal UI pathways first,
- will not invest heavily in technical bypasses immediately.

### 3.2 Opportunistic bypass
The user may try:
- uninstalling from Settings,
- disabling a browser extension,
- stopping the service,
- changing DNS,
- installing another browser,
- using a VPN,
- using search engines or social sites instead of explicit sites.

### 3.3 Deliberate administrative bypass
Out of scope for day-to-day design:
- reinstalling Windows,
- recovery mode,
- safe mode,
- deleting system files,
- manual registry and policy edits,
- advanced service manipulation.

## 4. Security goals

- Make easy bypasses fail.
- Make normal uninstall paths go through the unlock process.
- Detect component removal rapidly.
- Default to a protected or degraded state rather than silently failing open.
- Keep private data local.

## 5. Attack surfaces

- Windows service control.
- Browser extension install/remove path.
- DNS/proxy configuration.
- Local API authentication.
- Configuration files.
- SQLite database.
- Installer/uninstaller.
- Scheduled tasks.
- Local file system.
- UI actions.
- Browser policies.
- System clock.

## 6. Bypass classes and mitigations

### 6.1 Normal uninstall
Mitigation:
- custom uninstaller,
- lock timer,
- delayed multi-stage unlock,
- service-side authorization checks.

### 6.2 Extension removal
Mitigation:
- managed browser policies where supported,
- periodic extension health checks,
- degraded-mode response when missing.

### 6.3 Service stop
Mitigation:
- watchdog,
- automatic restart,
- health reporting,
- degraded mode when restart fails.

### 6.4 DNS change
Mitigation:
- periodic DNS verification,
- restore known configuration,
- block web access when DNS protection is broken.

### 6.5 VPN bypass

A VPN can route traffic (including DNS) through a remote tunnel, bypassing local network-level controls.

**Per-layer analysis:**

| Layer | Effective with VPN? | Reason |
|---|---|---|
| DNS Filter | ❌ No | Most VPNs route DNS through their own servers, bypassing `127.0.0.1:53`. |
| Local Proxy | ⚠️ Partial | Works only if the browser's proxy setting still points to localhost. Many VPNs override proxy settings. |
| Browser Extension | ✅ Yes | Runs inside the browser process, inspects URLs/DOM regardless of network path. **Primary VPN-resistant layer.** |
| Integrity Engine | ✅ Yes | Can detect that a VPN adapter is active and log the event. Cannot prevent VPN usage without kernel drivers (out of scope). |

Mitigation:
- The browser extension is the primary defense when a VPN is active.
- The integrity engine detects VPN adapters and logs a warning.
- DNS and proxy layers are documented as ineffective when a VPN overrides their configuration.
- The system does **not** attempt to block VPN software (outside threat model scope).
- Consider notifying the user when VPN usage is detected and DNS protection is bypassed.

### 6.6 Search keyword bypass
Mitigation:
- query inspection,
- title inspection,
- DOM monitoring,
- site-specific rules.

### 6.7 Data tampering
Mitigation:
- file signatures,
- checksums,
- backups,
- schema validation.

### 6.8 Clock manipulation

The commitment lock relies on elapsed time. If the system clock is advanced, the lock could expire prematurely.

Mitigation:
- Use monotonic elapsed time (`QueryUnbiasedInterruptTime` or `Stopwatch.GetElapsedTime`) instead of wall-clock time for lock calculations.
- Persist elapsed monotonic ticks in the database alongside wall-clock timestamps.
- On each lock check, compare wall-clock delta against monotonic delta. If they diverge by more than 5 minutes (allowing for sleep/hibernate), log `AEGIS-3006` (clock manipulation detected).
- Cross-reference with NTP when network is available to confirm actual time.
- **Never advance the lock timer based on wall-clock jumps alone.**
- If the system was asleep/hibernated, `QueryUnbiasedInterruptTime` excludes sleep time — use this to avoid counting sleep as elapsed lock time, or use a bias-inclusive counter if sleep time should count.

Severity: **Critical.** This is the highest-priority timer integrity control.

## 7. Fail-closed policy

If a required component is missing and repair fails:
- do not silently disable protection,
- do not assume safe browsing,
- enter configured degraded mode,
- display the reason clearly.

## 8. Out of scope

Aegis does not attempt to guarantee absolute prevention against an owner who deliberately chooses to bypass all software controls using advanced system administration or OS reinstall.
