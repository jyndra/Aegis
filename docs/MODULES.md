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
Status: **Implemented (Milestone 2)** in `Aegis.Infrastructure/Dns`. Async UDP proxy server on `127.0.0.1:53`, SQLite `domain_blocklist` & `HashSet` lookups, custom rules, and upstream forwarding.
Responsibilities:
- blocklists,
- custom domains,
- domain scoring,
- cached resolution,
- protection restoration.

## 5. Proxy module [EXTENDED]
Status: **Implemented (Milestone 7)** in `Aegis.Infrastructure/Proxy/ProxyServer.cs`. Async TCP proxy listener bound to dedicated dev port `19080` (preventing conflicts with Docker, Kubernetes, Streamlit, and ML dev tools), HTTP request line parser, CONNECT tunneling, and Rule Engine interception.
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
Status: **Implemented (Milestone 3)** in `Aegis.Infrastructure/Rules/RuleEngine.cs`. Multi-stage priority pipeline (Domain Blocklist -> Regex Heuristics -> Keyword Scoring), score threshold checking (70), and 5ms execution budget enforcement.
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
Status: **Dev Bootstrap Implemented (Milestone 1)** in `scripts/dev-setup.ps1`.
Responsibilities:
- install service,
- set up policies,
- deploy extension,
- initialize database,
- generate keys,
- configure defaults.

## 14. Uninstaller module [CORE]
Responsibilities:
- verify unlock eligibility,
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

## 17. Future AI modules [EXTENDED]

Deferred to Milestone 8. See ROADMAP.md.
Responsibilities:
- text classification,
- image classification,
- decision augmentation,
- model health checks.

## 18. Module boundaries
No module should:
- bypass another module's interface,
- mutate another module's state directly,
- depend on UI behavior,
- assume a component exists without verifying it.
