# Aegis Modules

> Module numbering and CORE/EXTENDED classification align with ARCHITECTURE.md §2 (Module Registry), which is the single source of truth.

## 1. UI module [CORE]
Status: **Dashboard Shell Implemented (Milestone 1)** in `Aegis.UI`. Live WinUI 3 control panel displaying subsystem health cards.
Responsibilities:
- dashboard,
- status cards,
- lock countdown,
- settings views,
- unlock workflow,
- diagnostics.

## 2. Service module [CORE]
Status: **Implemented (Milestone 1)** in `Aegis.Service`. Hosts Generic Host / Windows Service lifecycle, ASP.NET Core REST API, and SQLite database migration bootstrap.
Responsibilities:
- boot-time startup,
- policy enforcement,
- local API,
- module coordination,
- state persistence,
- event logging.

## 3. Watchdog module [CORE]
Status: **Skeleton Implemented (Milestone 0)** in `Aegis.Watchdog`. Separate Windows Service host process.
Responsibilities:
- heartbeat monitoring,
- service restart,
- tamper detection,
- failure escalation.

## 4. DNS module [CORE]
Status: **Hardened (Milestone 10)** in `Aegis.Infrastructure/Dns/DnsFilter.cs`. Async UDP proxy server on `127.0.0.1:53`, SQLite `domain_blocklist` lookups, custom rules, and upstream forwarding. M10 hardening: `volatile` field with `Volatile.Write` for thread-safe atomic blocklist swap; `CancellationTokenSource` disposal leak fixed in `StopAsync`.
Responsibilities:
- blocklists,
- custom domains,
- domain scoring,
- cached resolution,
- protection restoration.

## 5. Proxy module [EXTENDED]
Status: **Hardened (Milestone 10)** in `Aegis.Infrastructure/Proxy/ProxyServer.cs`. Async TCP proxy listener on dedicated dev port `19080`. M10 hardening: `SemaphoreSlim(50, 50)` concurrency gate prevents connection-flood DoS (HTTP 503 when gate full); `CancellationTokenSource` disposal leak fixed in `StopAsync`.
Responsibilities:
- optional HTTPS-aware filtering,
- search query inspection when available,
- page-level blocking,
- degraded-mode redirection.

## 6. Browser extension module [CORE]
Status: **Implemented (Milestone 4)** in `extension/`. Manifest V3 Chromium extension with background service worker (`background.ts`), HMAC handshake, token storage in `chrome.storage.local`, alarm heartbeats, SPA router inspector (`content.ts`), and local dark mode block page (`block.html`).
Responsibilities:
- inspect browser-visible signals,
- apply page blocking,
- communicate with service,
- report health,
- render block pages.

## 7. Rule engine module [CORE]
Status: **Hardened (Milestone 10)** in `Aegis.Infrastructure/Rules/RuleEngine.cs`. Multi-stage priority pipeline (Domain Blocklist → Regex Heuristics → Keyword Scoring → optional AI Boost), score threshold checking (70), and configurable execution budget. M10 hardening: exception handler now returns `Block` (fail-closed) per RECOVERY.md Principle 1 — "Fail closed. When in doubt, block." Timeout still returns `Allow` (content was unevaluated, not confirmed harmful).
Responsibilities:
- evaluate rules in priority order,
- normalize inputs,
- combine scoring sources,
- produce final decision.

## 8. Regex engine module [CORE]
Status: **Implemented (Milestone 3)** in `Aegis.Infrastructure/Rules/RegexEngine.cs`. Compiled regex heuristics, ReDoS protection via strict `MatchTimeout = 5ms`, and JSON policy reloading (`policies/regex-default.json`).
Responsibilities:
- domain token matching,
- URL pattern scoring,
- keyword prefixes and suffixes,
- curated porn domain heuristics.

## 9. Keyword engine module [CORE]
Status: **Implemented (Milestone 3)** in `Aegis.Infrastructure/Rules/KeywordEngine.cs`. URL query parameter extraction, page title inspection, word-boundary (`\b`) matching, and JSON policy reloading (`policies/keywords-default.json`).
Responsibilities:
- keyword matching,
- search-query evaluation,
- dynamic blacklist packs,
- site-aware keyword policies.

## 10. Integrity module [CORE]
Status: **Implemented (Milestone 6)** in `Aegis.Infrastructure/Integrity/IntegrityEngine.cs`. Boot-time audit (SQLite PRAGMA, HMAC row signatures, binary file hashes, subsystem baselines), periodic 5-minute background audit, self-healing policy restoration, and degraded-mode state transition.
Responsibilities:
- startup audits,
- periodic audits,
- self-healing,
- degraded-mode transitions,
- tamper logs.

## 11. Commit-lock module [CORE]
Status: **Implemented (Milestone 5)** in `Aegis.Infrastructure/Commitment/CommitLockEngine.cs`. 25-day lock state persistence in SQLite `lock_state`, monotonic clock anti-tampering (`Stopwatch.GetTimestamp()`), 3-stage unlock workflow with mandatory 48-hour cooldown, and rate limiting.
Responsibilities:
- 25-day timer,
- unlock requests,
- staged confirmations,
- uninstall gating,
- rate limiting.

## 12. Storage module [CORE]
Status: **Implemented (Milestone 1 & 2)** in `Aegis.Infrastructure/Storage`. `SqliteStorageService` (WAL mode, PRAGMA integrity_check, backup/restore), `DatabaseMigrator` (schema v1), `BlocklistRepository` (domain blocklist & hashes), `ModuleHealthRepository`, `EventRepository`.
Responsibilities:
- SQLite access,
- config persistence,
- event persistence,
- schema versioning,
- backup/restore.

## 13. Installer module [CORE]
Status: **Implemented (Milestone 9)** in `Aegis.Infrastructure/Deployment/InstallerService.cs` (plus dev bootstrap in `scripts/dev-setup.ps1`). Automates clean folder hierarchy deployment (`%ProgramData%\Aegis`), default policy JSON generation, policy restoration, and SQLite database migration.
Responsibilities:
- install service,
- set up policies,
- deploy extension,
- initialize database,
- generate keys,
- configure defaults.

## 14. Uninstaller module [CORE]
Status: **Implemented (Milestone 9)** in `Aegis.Infrastructure/Deployment/UninstallerService.cs`. Strictly gates uninstallation by querying `ICommitLockEngine`. If an active 25-day commitment device lock is running, uninstallation removal actions are rejected immediately.
Responsibilities:
- verify unlock eligibility via commitment lock engine,
- run staged confirmations,
- reverse installer changes,
- remove service and files cleanly.

## 15. Security module [CORE]
Status: **Helpers Implemented (Milestone 1)** in `Aegis.Infrastructure/Security/SecurityService.cs`. Windows DPAPI data protection (`ProtectData`/`UnprotectData`) and HMAC-SHA256 calculation/verification.
Responsibilities:
- local auth,
- token issuance,
- signature checks,
- file integrity,
- permission validation.

## 16. Logging module [CORE]
Status: **Implemented (Milestone 0)** using Serilog (console + daily rolling log files in `%ProgramData%\Aegis\logs\`).
Responsibilities:
- structured logs,
- rotation,
- diagnostics,
- event correlation.

## 17. AI modules [EXTENDED]
Status: **Implemented (Milestone 8 Option B)** in `Aegis.Infrastructure/Ai/`. Features the **3-Tier Cascade Gate**: Gate 1 (<0.1ms size bypass for UI icons), Gate 2 (<2ms YCbCr skin-tone pre-filter), and Gate 3 (CPU throttled `OnnxInferenceEngine` deep learning vision model to distinguish explicit anatomy from benign portraits or beach photography without browsing lag). Integrated into `RuleEngine` for Stage 4 AI boosting.
Responsibilities:
- 3-tier high-accuracy image classification,
- N-gram text classification,
- decision augmentation,
- CPU-throttled visual model inference.

## 19. Custom Policy module [CORE]
Status: **Implemented** in `Aegis.Infrastructure/Rules/CustomPolicyService.cs`. Provides provisions for custom websites, custom keywords, and custom regex rules with hot-reloading in DNS, Keyword, and Regex engines. Enforces the **One-Way Protection Ratchet**: adding rules is permitted while locked, but modifying or deleting rules is strictly blocked during the 25-day commitment period.
Responsibilities:
- custom website blocklist additions,
- custom keyword additions,
- custom regex pattern validation & compilation,
- protection ratchet enforcement (locked addition / post-cooldown modification),
- test mode uninstallation authorization bypass (`BypassLockForTesting`).

## 18. Module boundaries
No module should:
- bypass another module's interface,
- mutate another module's state directly,
- depend on UI behavior,
- assume a component exists without verifying it.
