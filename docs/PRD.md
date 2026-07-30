# Aegis Product Requirements Document

## 1. Overview

Aegis is a personal, local-first content filtering and commitment-device system for Windows. It is designed to reduce impulsive access to pornographic content by combining multiple enforcement layers: DNS filtering, browser extension filtering, local proxy filtering, keyword and regex scoring, optional AI-based classification, and system integrity verification.

Aegis is not intended to be a parental-control suite, enterprise DLP product, or anti-tamper malware. It is a user-owned system that prioritizes behavioral friction, transparent operation, and a documented recovery path.

## 2. Product goals

1. Block known pornographic domains.
2. Block pornographic searches and NSFW content on otherwise non-pornographic sites.
3. Continue filtering when a VPN is used whenever technically feasible.
4. Make disabling protection require deliberate, sustained effort.
5. Verify system integrity at startup and continuously during runtime.
6. Fail closed when critical protection components are missing.
7. Keep all data local by default.
8. Remain modular, maintainable, and testable.

## 3. Non-goals

Aegis does not attempt to:
- make itself impossible to remove by a machine owner,
- defeat recovery mode, safe mode, or Windows reinstall,
- perform covert surveillance,
- send user data to cloud services by default,
- protect against a determined administrator willing to spend hours undoing it.

## 4. Threat model

Aegis is designed to stop:
- impulsive uninstall attempts,
- normal Windows Settings uninstalls,
- browser extension removal,
- task killing,
- DNS changes,
- search keyword browsing,
- Reddit NSFW browsing,
- browser switching,
- portable browser bypasses,
- VPN-based bypasses within supported enforcement paths.

## 5. Core user experience

State names below align with the canonical state model in ARCHITECTURE.md §6.

### 5.1 Protected state
When all protection components are healthy (base state: `Protected`), the user sees a dashboard showing:
- protection status,
- lock timer (if `Locked` flag is active),
- blocked sites,
- blocked searches,
- service health,
- browser extension health,
- DNS/proxy health,
- recent events.

### 5.2 Locked flag
When the commitment timer is active (`Locked` flag on top of any base state):
- uninstall is disabled,
- settings changes are restricted,
- allowlist changes are restricted,
- protection components remain enforced.

### 5.3 Unlock Pending state
When the lock expires and the user requests unlock (base state: `Unlock Pending`), the user must complete a delayed unlock workflow with multiple confirmations and cooldowns before uninstall or disable is allowed.

### 5.4 Degraded state
If a critical component is removed or disabled (base state: `Degraded`):
- Aegis attempts restoration (transitions to `Recovery`).
- If restoration fails, the system remains in `Degraded` mode with fail-closed behavior: blocks web access or routes to a local status page.

### 5.5 Disabled state
After completing the full unlock workflow (base state: `Disabled`):
- Protection is inactive.
- The user may uninstall or re-enable protection.

## 6. Functional requirements

### 6.1 DNS filtering
- Support local DNS proxying.
- Support importing and updating blocklists.
- Support custom domain blacklists.
- Cache results efficiently.
- Preserve local logs of decisions.

### 6.2 Domain intelligence
- Score domains using regex, heuristics, and domain-token rules.
- Flag newly registered pornographic domains using configurable rules.
- Support custom keyword and pattern packs.

### 6.3 Keyword filtering
- Inspect search queries, page titles, URLs, and selected metadata.
- Block or warn based on keyword policies.
- Support exact, prefix, suffix, and fuzzy matches.

### 6.4 Browser extension
- Manifest V3 extension for Chromium browsers.
- Inspect URLs and page metadata.
- Communicate securely with the local service.
- Render block pages.
- Report health and handshake status.

### 6.5 Local service
- Run at startup.
- Manage policy decisions.
- Expose authenticated local API.
- Coordinate modules.
- Log events.
- Enforce fail-closed behavior.

### 6.6 Integrity verification
- Verify installed components at boot and periodically.
- Detect missing or disabled extension, service, DNS configuration, proxy config, databases, and secure files.
- Attempt self-healing when supported.
- Enter degraded mode when repair fails.

### 6.7 Commitment device
- Default lock period: 25 days.
- Delayed disable/uninstall path.
- Multi-stage confirmation with cooldowns.
- Optional typed acknowledgements.
- Rate limit unlock attempts.

### 6.8 UI
- Minimal, readable, and calm.
- Dashboard first.
- Status cards for each subsystem.
- Clear lock countdown.
- Clear degraded-state explanations.

## 7. Non-functional requirements

- Local-first.
- Low overhead.
- Reliable startup.
- Clear diagnostics.
- Strong test coverage.
- Maintainable architecture.
- Fast filtering decisions.
- Structured logs.
- Safe recovery path.

## 8. Acceptance criteria

Aegis is acceptable when:
- known porn domains are blocked,
- keyword searches on major sites are intercepted,
- the browser extension and service can be verified after boot,
- degraded mode activates when protection is missing,
- the normal Windows uninstall path is replaced with the designed unlock flow,
- logs and dashboards show the reason for each decision,
- the codebase is modular and documented.

## 9. Platform requirements

- **Minimum Windows version:** Windows 10 21H2 (build 19044) or later.
- **Target browsers:** Google Chrome, Microsoft Edge (Tier 1). Brave, Vivaldi (Tier 2). See BROWSER_MATRIX.md.
- **Runtime:** .NET 8+ (LTS). See TECH_STACK.md.
- **UI framework:** WinUI 3 (Windows App SDK). Requires Windows 10 1809+ (satisfied by the 21H2 minimum).
