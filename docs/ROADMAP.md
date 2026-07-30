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
- Manifest V3 extension.
- Authentication handshake.
- URL/title/query inspection.
- Block page rendering.

## Milestone 5: Commitment device
- 25-day lock.
- Disable/uninstall gating.
- Multi-stage confirmation workflow.
- Cooldown and rate limiting.

## Milestone 6: Integrity framework
- Boot-time audit.
- Continuous health checks.
- Tamper detection.
- Self-healing where supported.

## Milestone 7: Proxy support
- Optional system-wide or browser-wide proxy.
- HTTPS-aware filtering for supported cases.

## Milestone 8: Advanced detection
- Optional AI text classification.
- Optional NSFW image detection.

## Milestone 9: Installer and uninstaller
- Clean install flow.
- Clean reverse flow.
- Policy deployment and restoration.

## Milestone 10: Hardening
- performance tuning,
- recovery tests,
- failure mode tests,
- comprehensive documentation.

## Release criterion
A milestone is complete only when:
- code is implemented,
- tests pass,
- docs are updated,
- manual verification is recorded,
- rollback path is clear.
