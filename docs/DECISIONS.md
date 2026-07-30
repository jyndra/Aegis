# Aegis Architectural Decisions

## 1. Windows only for v1
Decision: Build Windows-first.
Reason: the commitment-device and service model are most practical there.

## 2. Local-first persistence
Decision: Keep data local.
Reason: privacy and reliability.

## 3. Browser extension plus service
Decision: Use both.
Reason: extension handles browser-visible signals; service handles system-wide enforcement.

## 4. Fail closed
Decision: Default to blocking or degraded mode when critical components are missing.
Reason: prevents silent bypass.

## 5. Custom unlock workflow
Decision: Replace normal uninstall with a staged unlock and uninstall flow.
Reason: creates behavioral friction.

## 6. SQLite
Decision: Use SQLite for state.
Reason: simple, local, dependable, easy to inspect.

## 7. Clean architecture
Decision: Separate domain, application, and infrastructure.
Reason: long-term maintainability.

## 8. Managed browser policies where supported
Decision: Prefer supported browser management for force-install style behavior.
Reason: better stability than unsupported hacks.

## 9. No cloud dependency in v1
Decision: Keep the first version offline-capable.
Reason: reliability and privacy.

## 10. Incremental milestones
Decision: Build in stages.
Reason: avoid a tangled monolith.
