# Aegis — Personal Content Filtering & Commitment System

Aegis is a personal, local-first content filtering and commitment-device system for Windows (10 21H2+ / 11). It is designed to reduce impulsive access to pornography through multi-layer enforcement (DNS proxy, MV3 browser extension, rule & keyword engine, commitment lock timer, and integrity verification).

## System Architecture

Aegis uses Clean Architecture principles with explicit module boundaries:

- **Aegis.Core**: Domain models, interfaces, configuration options, error taxonomy (no external dependencies).
- **Aegis.Infrastructure**: Implementation of filtering engines, SQLite storage, cryptographic security, integrity auditing, and time abstractions.
- **Aegis.Service**: Windows Service host and local REST API (`https://127.0.0.1:9443`).
- **Aegis.Watchdog**: Secondary monitor service for main service resilience.
- **Aegis.UI**: WinUI 3 dashboard for system status, lock countdown, and diagnostics.
- **Extension**: MV3 Chromium extension for Chrome, Edge, and Brave.

## Project Structure

```
├── src/
│   ├── Aegis.Core/            # Domain interfaces & models
│   ├── Aegis.Infrastructure/  # Subsystem implementations
│   ├── Aegis.Service/         # Background service & HTTP API host
│   ├── Aegis.Watchdog/        # Watchdog service
│   └── Aegis.UI/              # WinUI 3 Dashboard app
├── tests/                     # xUnit test suites
├── extension/                 # MV3 Browser Extension (TypeScript)
├── scripts/                   # Bootstrap scripts for development
└── docs/                      # Architectural specs & design docs
```

## Quick Start (Development)

### Prerequisites

- .NET 8.0 SDK (Windows 10 21H2+ or Windows 11)
- Node.js 18+ (for building browser extension)
- PowerShell 7+

### Building .NET Solution

```powershell
# Restore and build the solution
dotnet build Aegis.sln

# Run unit tests
dotnet test
```

### Development Setup Script

To bootstrap local development directories and SQLite database scaffolding:

```powershell
.\scripts\dev-setup.ps1
```

## Documentation

Full architectural specifications, threat models, API references, and design reviews are available in [/docs](docs/):

- [ARCHITECTURE.md](docs/ARCHITECTURE.md)
- [PRD.md](docs/PRD.md)
- [TECH_STACK.md](docs/TECH_STACK.md)
- [CONFIG.md](docs/CONFIG.md)
- [API.md](docs/API.md)
- [DATABASE.md](docs/DATABASE.md)
- [ERRORS.md](docs/ERRORS.md)
- [RECOVERY.md](docs/RECOVERY.md)
- [BROWSER_MATRIX.md](docs/BROWSER_MATRIX.md)
- [EXTENSION_CONSTRAINTS.md](docs/EXTENSION_CONSTRAINTS.md)
- [TESTING.md](docs/TESTING.md)

## License

Personal project — strictly local-first and user-owned.
