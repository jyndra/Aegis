# Master Prompt for Coding Agent

You are a senior Windows systems engineer, browser extension engineer, backend engineer, security engineer, and software architect.

Build Aegis as a local-first Windows application for personal self-control and pornography filtering. Use the docs in this repository as the source of truth. Read all documentation before coding. Treat the docs as the specification.

## Core mission
Create a multi-layer filtering and commitment-device system that:
- blocks known porn domains,
- blocks pornographic searches and NSFW content on otherwise normal sites,
- maintains protection across browser changes and VPN usage whenever technically feasible,
- verifies system integrity at boot and continuously,
- makes normal uninstall/disable paths go through a deliberate unlock workflow,
- fails closed when critical components are missing,
- remains modular and maintainable.

## Required architecture
Implement separate modules for:
- Windows Service
- Watchdog
- Browser Extension
- DNS Filter
- Optional Proxy
- Rule Engine
- Keyword Engine
- Regex Engine
- Integrity Engine
- Commit-Lock Engine
- SQLite persistence
- Local API
- Installer/Uninstaller
- UI
- Logging

## Required product behavior
- Use a 25-day default lock timer.
- Replace immediate uninstall with a staged, delayed unlock flow.
- Verify that all critical components are present on boot and at intervals.
- If a critical component is missing, attempt self-healing using supported mechanisms.
- If repair fails, enter configurable protection-degraded mode.
- Keep all data local by default.
- Produce structured logs and clear health reports.

## Rule priorities
1. Known domain blocklists
2. Regex and heuristics
3. Keyword filtering
4. Optional AI text classification
5. Optional NSFW image detection
6. Final decision

## Code quality requirements
- Clean architecture
- Strong typing
- Small focused modules
- Dependency injection
- Minimal global state
- Tests for each module
- Document every public interface
- Keep the codebase easy to extend

## Development approach
Work in milestones. Implement only the current milestone. Do not try to finish everything at once. Each milestone must end with:
- working code,
- tests,
- docs updates,
- manual verification notes.

## Safety and recoverability
Do not implement destructive or hidden behavior. Do not break Windows recovery or data safety. The system should be strong against impulsive disabling, but still recoverable by the owner of the machine through the designed unlock flow.

## Start here
1. Create the repository skeleton.
2. Implement configuration, logging, and database scaffolding.
3. Implement the service skeleton and UI shell.
4. Add tests and docs.
5. Then proceed milestone by milestone.
