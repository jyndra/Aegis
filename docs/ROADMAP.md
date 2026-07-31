# Aegis Roadmap

## Milestone 0: Repository foundation
- [x] Create project structure (.NET solution + 6 projects + 3 test projects).
- [x] Add docs (19 architecture and design specifications).
- [x] Add linting, formatting (.editorconfig), and testing setup (xUnit, Moq, FluentAssertions).
- [x] Add config loader (`appsettings.json`) and logging skeleton (Serilog).
- [x] **Dev bootstrap script:** a PowerShell script (`scripts/dev-setup.ps1`).

## Milestone 1: Service skeleton
- [x] Windows Service skeleton (`AegisBackgroundService`, runnable in-process or via `sc.exe`).
- [x] Health reporting (`HealthReporter` & `ModuleHealthRepository`).
- [x] Local API stub (minimal APIs hosting `/health`, `/status/report`, `/policy`, `/handshake`, `/evaluate`, `/unlock/*`, `/integrity/*`).
- [x] SQLite initialization (`SqliteStorageService` with WAL mode, integrity check, `DatabaseMigrator` v1 schema).
- [x] UI shell (`Aegis.UI` WinUI 3 control panel).
- [x] **Dev bootstrap update:** `dev-setup.ps1` with `-RegisterService` / `-UnregisterService` flags and browser extension guidance.

## Milestone 2: DNS filtering
- [x] Local DNS module (`DnsFilter` async UDP listener on `127.0.0.1:53`).
- [x] Blocklist ingestion (`domain_blocklist` SQLite table & in-memory `HashSet` lookups).
- [x] Custom domain rules (`policies/custom-blacklist.txt`).
- [x] Basic decision logging (`events` table logging for DNS blocks/allows).

## Milestone 3: Rule engine
- [x] Regex engine (`RegexEngine` with ReDoS `MatchTimeout` protection).
- [x] Keyword engine (`KeywordEngine` URL parameter & title inspection).
- [x] Scoring system (`RuleEngine` score thresholding & 5ms budget enforcement).
- [x] Allow/block/redirect decisions (`EvaluationResult` pipeline).

## Milestone 4: Browser extension
- [x] Manifest V3 extension (`extension/` background service worker, content script, block.html).
- [x] Authentication handshake (HMAC handshake, `POST /handshake`, JWT token cached in `chrome.storage.local`).
- [x] URL/title/query inspection (Content script inspecting `document.title`, search params & SPA `history.pushState`).
- [x] Block page rendering (Local dark mode `block.html` rendering upon `FilterDecision.Block`).

## Milestone 5: Commitment device
- [x] 25-day lock (`CommitLockEngine` 25-day active lock state in SQLite `lock_state`).
- [x] Disable/uninstall gating (Locked state blocks disable/uninstall requests).
- [x] Multi-stage confirmation workflow (3-stage unlock workflow with 48h cooling-off period).
- [x] Cooldown and rate limiting (Max 3 failed attempts, 24h exponential lockout).

## Milestone 6: Integrity framework
- [x] Boot-time audit (`IntegrityEngine` SQLite PRAGMA, HMAC row signatures, file hashes, subsystem baselines).
- [x] Continuous health checks (Periodic background audit loop running every 5 minutes).
- [x] Tamper detection (Detects missing policy files, modified binary hashes, or SQLite table tampering).
- [x] Self-healing where supported (Auto-restores default policy JSON files & WAL checkpointing).

## Milestone 7: Proxy support
- [x] Optional system-wide or browser-wide proxy (`ProxyServer` async TCP listener on isolated dev port `19080`).
- [x] HTTPS-aware filtering for supported cases (HTTP GET/POST inspection and HTTPS `CONNECT` tunnel domain evaluation).

## Milestone 8: Advanced detection
- [x] Optional AI text classification (`AiTextClassifier` N-gram & Bayesian token analyzer).
- [x] Optional NSFW image detection (`NsfwImageClassifier` YCbCr skin-tone and edge entropy analyzer).

## Milestone 9: Installer and uninstaller
- [x] Clean install flow (`InstallerService` directory architecture and database migration).
- [x] Clean reverse flow (`UninstallerService` with strict 25-day commitment device lock gating).
- [x] Policy deployment and restoration (Default `keywords-default.json` and `regex-default.json` deployment & restore logic).

## Milestone 10: Hardening
- [x] Performance tuning (`ProxyServer` SemaphoreSlim(50) concurrency gate, `DnsFilter` volatile thread-safe blocklist swap, duplicate config binding eliminated).
- [x] Recovery tests (`RecoveryTests.cs`: policy self-healing, corruption overwrite, custom rule preservation, commitment lock rejection, clean reverse flow).
- [x] Failure mode tests (`FailureModeTests.cs`: fail-closed `RuleEngine` on exception, allow-on-timeout, DNS upstream graceful degradation, null guard, rate-limit contract).
- [x] Custom policy provisions & zero-friction test mode (`CustomPolicyService`: custom website, keyword, regex provisions; One-Way Protection Ratchet; `bypassLockForTesting: true` zero-friction test mode; production 10-step 5-minute per-step cooldown challenge; `CustomPolicyTests.cs`).
- [x] Comprehensive documentation (all `/docs` updated, `TECH_STACK.md` finalized, hardening notes in `MODULES.md`).

## Release criterion
A milestone is complete only when:
- code is implemented,
- tests pass,
- docs are updated,
- manual verification is recorded,
- rollback path is clear.
