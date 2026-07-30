# Aegis Testing Strategy

## 1. Test categories

### 1.1 Unit tests
- Test individual classes and methods in isolation.
- All domain logic, rule engine scoring, keyword matching, regex evaluation, config validation, state transitions, and HMAC verification.
- No I/O, no database, no network, no Windows APIs.
- Target: every module has unit tests covering its public interface.

### 1.2 Integration tests
- Test module interactions through their interfaces.
- Database read/write via in-memory SQLite.
- API endpoint tests via `WebApplicationFactory` (ASP.NET Core test host).
- Named pipe client/server round-trip.
- Config loading from file system (using temp directories).

### 1.3 System tests
- Full-stack tests with the service running locally (not installed as a Windows Service).
- Extension-to-service handshake (using a test browser profile via Playwright).
- DNS query-to-block-decision round-trip.
- Lock timer enforcement.
- Degraded mode activation on component removal.

### 1.4 Manual verification
- Required at each milestone.
- Documented in a `VERIFICATION.md` per milestone.
- Covers: Windows Service install/start/stop, extension install in Chrome/Edge, DNS redirection, UI dashboard rendering, uninstall flow.

## 2. Tooling

| Purpose | Tool |
|---|---|
| Test framework | xUnit |
| Mocking | Moq |
| Assertions | FluentAssertions |
| API testing | `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory`) |
| Browser testing | Playwright for .NET |
| Code coverage | Coverlet + ReportGenerator |
| Test runner | `dotnet test` |
| Extension testing | Playwright with Chrome extension loading |

## 3. Service testing without installation

The Windows Service uses `Microsoft.Extensions.Hosting`. All service logic is in a hosted service class (`AegisService : BackgroundService`) that can be started in-process during tests without registering as a real Windows Service.

Pattern:
```
Host.CreateDefaultBuilder()
    .ConfigureServices(services => {
        services.AddHostedService<AegisService>();
        // register test doubles
    })
    .Build()
    .StartAsync();
```

This allows integration tests to exercise the full service lifecycle without elevation or SCM registration.

## 4. DNS testing

The DNS module binds to a configurable address/port. In tests:
- Bind to `127.0.0.1:15353` (non-privileged port).
- Send test DNS queries via `DnsClient.NET`.
- Assert blocked domains return `0.0.0.0` / `NXDOMAIN`.
- Assert allowed domains are forwarded and resolved.

No system DNS changes are needed for testing.

## 5. Extension testing

Use Playwright to:
1. Launch Chrome with `--load-extension=path/to/extension`.
2. Navigate to test URLs.
3. Assert block page is rendered for blocked domains.
4. Assert handshake completes with a running test service.
5. Assert degraded behavior when the test service is stopped.

Extension unit tests (keyword matching, message handling) run in Node.js via `vitest` or `jest` without a browser.

## 6. Mock boundaries

| Real in tests | Mocked in unit tests |
|---|---|
| Rule engine logic | Database access (use in-memory repo) |
| Keyword matching | File system (use virtual fs or temp dirs) |
| Regex evaluation | Windows Service APIs |
| Config validation | Network (DNS upstream, NTP) |
| State machine transitions | Named pipe transport |
| HMAC computation | System clock (inject `TimeProvider`) |

Critical: **Always inject `TimeProvider`** (added in .NET 8) instead of using `DateTime.Now` or `DateTimeOffset.Now`. This enables deterministic testing of lock timers, cooldowns, and expiration logic.

## 7. Test data

- Blocklist fixtures: small lists (100 domains) checked into `tests/fixtures/`.
- Keyword fixtures: curated test keyword packs.
- Config fixtures: valid and invalid config JSON files.
- Database fixtures: pre-populated SQLite files for migration and corruption tests.

## 8. CI pipeline

Minimum viable CI (GitHub Actions):
```
- checkout
- dotnet restore
- dotnet build --no-restore
- dotnet test --no-build --collect:"XPlat Code Coverage"
- npm ci (extension)
- npm test (extension unit tests)
- upload coverage report
```

Extension Playwright tests run separately (require a display or xvfb).

## 9. Coverage targets

| Category | Target |
|---|---|
| Domain logic (rule engine, state machine, lock) | ≥ 90% |
| Infrastructure (database, file I/O, pipes) | ≥ 70% |
| API endpoints | ≥ 80% |
| Extension content scripts | ≥ 70% |
| Overall | ≥ 75% |

Coverage is tracked but not gated in CI initially. Gate at Milestone 10 (hardening).

## 10. Failure mode tests

Required tests per SECURITY.md §10:
- [ ] Service fails to start → degraded mode activates.
- [ ] Extension missing → integrity engine detects, logs, enters degraded mode.
- [ ] DNS config changed externally → integrity engine detects and restores.
- [ ] Service killed → watchdog restarts it.
- [ ] Database corrupted → backup restore attempted, degraded mode if restore fails.
- [ ] Invalid token submitted to API → 401 returned, event logged.
- [ ] Unlock flow expires mid-stage → progress reset, event logged.
- [ ] System clock jumped forward → lock timer cross-references monotonic time.
- [ ] System clock jumped backward → lock timer cross-references monotonic time.

## 11. Active test suites (Milestone 0–2)

The solution includes 12 test suites across `tests/`:

### Aegis.Core.Tests
- `AegisExceptionTests.cs`: Error code mapping and inner exception handling.
- `ConfigurationOptionsTests.cs`: Default option values and section name constants.
- `DomainModelsTests.cs`: Record equality, immutability, and property verification.

### Aegis.Infrastructure.Tests
- `DatabaseMigratorTests.cs`: In-memory SQLite DDL schema creation and v1 migration.
- `HealthReporterTests.cs`: Persistence to `module_health` SQLite table and fallback baselines.
- `ModuleHealthRepositoryTests.cs`: SQLite `UPSERT` conflict resolution.
- `SecurityServiceTests.cs`: HMAC-SHA256 calculation, fixed-time verification, tampering detection, and Windows DPAPI round-trip data protection.
- `SecurityServiceHandshakeTests.cs`: HMAC handshake authentication, timestamp skew rejection, and JWT token issuance & validation.
- `CommitLockEngineTests.cs`: 25-day lock state persistence, 3-stage unlock workflow, 48-hour cooling-off period validation, and rate limiting.
- `IntegrityEngineTests.cs`: Boot-time audit execution, SQLite PRAGMA checks, HMAC row signature verification, and self-healing policy restoration.
- `SqliteStorageServiceTests.cs`: File creation, WAL mode initialization, `PRAGMA integrity_check`, backup, and restore.
- `SystemTimeProviderTests.cs`: Monotonic tick calculations and UTC bounds.
- `DnsPacketTests.cs`: Binary header/question parsing, block response packet building, and compression pointer loop protection.
- `BlocklistRepositoryTests.cs`: Domain normalization and SQLite bulk ingestion.
- `DnsFilterTests.cs`: In-memory lookup validation and UDP server lifecycle tests.
- `ProxyServerTests.cs`: Dedicated dev port `19081` binding, HTTP request line evaluation, 403 Forbidden block response, and socket shutdown.
- `RegexEngineTests.cs`: Regex matching and heuristic scoring.
- `KeywordEngineTests.cs`: Search query parameter extraction and keyword matching.
- `RuleEngineTests.cs`: Multi-stage pipeline evaluation, domain blocklist priority, and threshold scoring with optional Stage 4 AI boosting.
- `AiTextClassifierTests.cs`: N-gram TF-IDF & Bayesian probability text classification for safe, suggestive, and explicit content.
- `NsfwImageClassifierTests.cs`: Verifies Option B 3-Tier Cascade Gate (Gate 1 icon bypass, Gate 2 YCbCr allow without neural net, and Gate 3 ONNX deep learning vision evaluation).
- `ConfigValidatorTests.cs`: Service configuration rules.
- `DependencyInjectionTests.cs`: Container resolution for `AddAegisInfrastructure()` registered services.

### Aegis.Service.Tests
- `AegisBackgroundServiceTests.cs`: Background service lifecycle, DB initialization call, and health reporting.
- `ServiceApiTests.cs` & `FullApiEndpointTests.cs`: `WebApplicationFactory` integration test suite covering `/health`, `/status/report`, `/policy`, `/handshake`, `/evaluate`, `/evaluate/ai/*`, `/unlock/*`, `/integrity/*`.
