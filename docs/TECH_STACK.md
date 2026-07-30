# Aegis Technology Stack

## 1. Service language

Decision: **C# / .NET 8+** (LTS).

Rationale:
- native Windows Service hosting via `Microsoft.Extensions.Hosting.WindowsServices`,
- strong typing and dependency injection out of the box,
- mature ecosystem for named pipes, HTTP, SQLite, cryptography,
- shared language with the UI framework,
- efficient AOT-publishing option for smaller deployment size.

## 2. UI framework

Decision: **WinUI 3** (Windows App SDK).

Rationale:
- modern Fluent Design language,
- XAML-based declarative UI,
- integrates with .NET 8,
- supports Windows 10 21H2+ and Windows 11,
- accessible by default (high contrast, keyboard nav, narrator).

The UI runs as a standard user-level process and communicates with the service over the local API.

## 3. Minimum Windows version

Decision: **Windows 10 21H2** (build 19044) or later.

Rationale:
- mainstream support baseline,
- required APIs for WinUI 3, named pipes, modern .NET hosting are available,
- avoids supporting end-of-life builds.

## 4. Browser extension

Decision: **Manifest V3 Chromium extension** (plain TypeScript, bundled with esbuild).

Rationale:
- MV3 is the only supported manifest version going forward on Chrome and Edge,
- TypeScript catches errors at build time,
- esbuild is fast and produces small bundles,
- no framework (React/Vue) needed for a content-script + service-worker extension.

Target browsers: Chrome, Edge, Brave (all Chromium-based).
Firefox: deferred to a future milestone (requires separate WebExtensions build).

## 5. DNS module

Decision: **Custom async UDP listener** using `System.Net.Sockets.UdpClient`.

Rationale:
- .NET 8 has high-performance socket APIs,
- a custom listener allows full control over blocklist matching and logging,
- avoids external native dependencies,
- domain lookups use an in-memory `HashSet<string>` populated from SQLite at startup.

The module listens on `127.0.0.1:53` and forwards non-blocked queries to the configured upstream DNS.

## 6. Proxy module

Decision: **Deferred to Milestone 7.** When implemented, use `YARP` (Yet Another Reverse Proxy) or `Titanium.Web.Proxy` as a local HTTP/HTTPS proxy.

Rationale:
- proxy is optional per ARCHITECTURE.md,
- HTTPS interception requires a custom root CA (opt-in only),
- YARP is a Microsoft-supported high-performance proxy library,
- Titanium.Web.Proxy has explicit MITM support if the user enables CA installation.

## 7. Persistence

Decision: **SQLite** via `Microsoft.Data.Sqlite`.

Rationale:
- lightweight, embedded, zero-config,
- WAL mode for concurrent reads,
- all writes funneled through the service process to avoid contention,
- schema migrations via a simple version table and sequential migration scripts.

## 8. Local API

Decision: **ASP.NET Core minimal APIs** hosted inside the Windows Service, bound to `https://127.0.0.1:{port}` with a self-signed localhost certificate.

Named pipes used for privileged IPC between service, watchdog, and installer.

Rationale:
- minimal APIs are lightweight and low-ceremony,
- ASP.NET Core integrates with the generic host used by Windows Services,
- self-signed cert avoids plaintext localhost traffic,
- named pipes for privileged ops can use Windows ACLs for access control.

## 9. Cryptography

Decision: Use `System.Security.Cryptography` and **Windows DPAPI** (`ProtectedData` class) for local key storage.

- HMAC-SHA256 for database row integrity (lock state, policy).
- DPAPI for encrypting the HMAC key at rest (ties to service account SID).
- Ed25519 or RSA-2048 for policy pack signatures (if signing is needed beyond HMAC).
- Short-lived HMAC-based tokens for API authentication.

## 10. Build and packaging

- **Build:** `dotnet build` / `dotnet publish`.
- **Testing:** `xUnit` + `Moq` + `FluentAssertions`.
- **Code quality:** `.editorconfig`, `dotnet format`, nullable reference types enabled.
- **Packaging:** MSIX for distribution (supports auto-update, SmartScreen, and code signing).
- **CI:** GitHub Actions (or local `dotnet test` for offline development).
- **Extension build:** `npm` + `esbuild` + `typescript`.

## 11. Key libraries

| Purpose | Library |
|---|---|
| Windows Service hosting | `Microsoft.Extensions.Hosting.WindowsServices` |
| HTTP API | ASP.NET Core Minimal APIs |
| SQLite | `Microsoft.Data.Sqlite` |
| Logging | `Serilog` (JSON file sink + optional Windows Event Log sink) |
| DI | `Microsoft.Extensions.DependencyInjection` |
| Configuration | `Microsoft.Extensions.Configuration` (JSON provider) |
| Named pipes | `System.IO.Pipes` |
| DPAPI | `System.Security.Cryptography.ProtectedData` |
| UI | `Microsoft.WindowsAppSDK` (WinUI 3) |
| Scheduling | `System.Threading.Timer` / `Cronos` for cron expressions |
| DNS parsing | `DnsClient.NET` (for upstream forwarding) or custom parser |
| Testing | `xUnit`, `Moq`, `FluentAssertions` |
| Extension bundling | `esbuild`, `typescript` |
