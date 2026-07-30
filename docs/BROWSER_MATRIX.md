# Aegis Browser Support Matrix

## 1. Purpose

Define per-browser support for extension installation, policy enforcement, and expected behavior.

## 2. Support tiers

| Tier | Definition |
|---|---|
| **Tier 1** | Fully supported. Extension, policies, and integrity checks all work. |
| **Tier 2** | Extension works. Policy force-install may not be available or reliable. |
| **Tier 3** | No extension support. Protection relies on DNS and proxy layers only. |

## 3. Browser matrix

| Browser | Tier | MV3 Extension | Force-install policy | Policy registry path | Notes |
|---|---|---|---|---|---|
| **Google Chrome** | 1 | ✅ | ✅ | `HKLM\SOFTWARE\Policies\Google\Chrome\ExtensionInstallForcelist` | Shows "managed by your organization" banner. User can delete the registry key with admin access. |
| **Microsoft Edge** | 1 | ✅ | ✅ | `HKLM\SOFTWARE\Policies\Microsoft\Edge\ExtensionInstallForcelist` | Same MV3 extension works. Separate policy namespace from Chrome. |
| **Brave** | 2 | ✅ | ⚠️ Partial | `HKLM\SOFTWARE\Policies\BraveSoftware\Brave\ExtensionInstallForcelist` | Brave sometimes ignores Chrome-style policies. Force-install may not persist across updates. Test per version. |
| **Vivaldi** | 2 | ✅ | ⚠️ Partial | Uses Chrome policy path (sometimes) | Behavior is inconsistent across versions. |
| **Opera / Opera GX** | 2 | ✅ (with flag) | ❌ | N/A | Requires enabling "Allow extensions from other stores" flag. No force-install policy support. |
| **Firefox** | 3 | ❌ (MV3 incompatible API differences) | ❌ | N/A | Firefox uses WebExtensions with different APIs. MV3 support exists but is not compatible with Chrome MV3. Requires a separate extension build (deferred). |
| **Portable browsers** | 3 | ❌ | ❌ | N/A | Portable browsers (e.g., PortableApps Chrome) do not read system registry policies. No extension install mechanism. |
| **Tor Browser** | 3 | ❌ | ❌ | N/A | Based on Firefox ESR. Does not support Chrome extensions. |
| **PWAs / WebView2** | 3 | ❌ | ❌ | N/A | No extension injection point. |

## 4. Mitigation by tier

### Tier 1 (Chrome, Edge)
- Full extension protection: URL blocking, keyword scanning, DOM monitoring, handshake.
- Force-install via registry policy (installer sets this up).
- Integrity engine monitors extension presence.
- If the extension is removed, the integrity engine detects it within 2 minutes and enters degraded mode.

### Tier 2 (Brave, Vivaldi, Opera)
- Extension can be manually installed from the Chrome Web Store.
- Force-install may or may not work — document this to the user.
- The integrity engine treats these as "best-effort" monitored browsers.
- If the extension cannot be verified, log the gap but do not enter degraded mode (user accepted the risk by using a Tier 2 browser).

### Tier 3 (Firefox, portable, Tor, PWAs)
- No extension protection.
- Rely on DNS blocking (catches domain-level requests).
- Rely on proxy blocking if enabled (catches URL patterns).
- Keyword and content-level filtering is **not available** for Tier 3 browsers.
- The integrity engine logs that a Tier 3 browser was detected (by process monitoring or network adapter analysis) and notifies the user.

## 5. Browser detection

The integrity engine should periodically scan for running browser processes:

| Browser | Process name(s) |
|---|---|
| Chrome | `chrome.exe` |
| Edge | `msedge.exe` |
| Brave | `brave.exe` |
| Firefox | `firefox.exe` |
| Opera | `opera.exe` |
| Vivaldi | `vivaldi.exe` |
| Tor | `firefox.exe` (in Tor Browser directory) |

Detection method:
1. Enumerate running processes.
2. Match against known browser process names.
3. For Tier 3 browsers, log a warning event if detected while protection is active.
4. For unknown executables making HTTP(S) connections, rely on DNS/proxy layers.

## 6. Future work

- Firefox WebExtension port (separate codebase, different APIs).
- Chromium browser auto-detection and policy deployment.
- Browser install monitoring (detect when a new browser is installed).

## 7. User communication

The UI dashboard should display:
- A list of detected browsers with their support tier.
- Per-browser protection status (extension installed / not installed / force-installed).
- Recommendations for Tier 2/3 browsers (e.g., "Consider using Chrome or Edge for best protection").
