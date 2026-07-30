# Aegis Roadmap

## Milestone 0: Repository foundation
- Create project structure.
- Add docs.
- Add linting, formatting, and testing setup.
- Add config loader and logging skeleton.
- **Dev bootstrap script:** a PowerShell script (`scripts/dev-setup.ps1`) that:
  - creates `%ProgramData%\Aegis\` directories,
  - initializes a default `appsettings.json`,
  - initializes an empty SQLite database with schema,
  - generates dev-mode HMAC keys (unprotected, for testing only).

## Milestone 1: Service skeleton
- Windows Service skeleton (runnable in-process for development, installable via `sc.exe` for integration testing).
- Health reporting.
- Local API stub.
- SQLite initialization.
- UI shell.
- **Dev bootstrap update:** extend `dev-setup.ps1` to register the service via `sc.exe` and load the extension unpacked in Chrome/Edge.

## Milestone 2: DNS filtering
- Local DNS module.
- Blocklist ingestion.
- Custom domain rules.
- Basic decision logging.

## Milestone 3: Rule engine
- Regex engine.
- Keyword engine.
- Scoring system.
- Allow/block/redirect decisions.

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
