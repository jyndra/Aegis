# Aegis Database Design

## 1. Storage choice

Use SQLite as the primary local store.

## 2. Database goals

- reliable local persistence,
- simple backups,
- predictable schema migrations,
- fast lookups for block decisions,
- audit trail support.

## 3. Main entities

### settings
Stores application configuration.

Fields:
- key
- value
- updated_at

### lock_state
Stores lock and unlock status. HMAC-protected (see §9).

Fields:
- id
- activated_at
- expires_at
- activated_monotonic_ticks (monotonic time at lock activation)
- elapsed_monotonic_ticks (last recorded elapsed monotonic ticks)
- last_tick_update_at (wall-clock time of last tick update)
- unlock_requested_at
- unlock_stage
- unlock_state
- last_change_at
- row_hmac

### events
Stores protection and tamper events.

Fields:
- id
- timestamp
- component
- event_type
- severity
- message
- details_json

### blocked_rules
Stores blocked domains, patterns, and keywords.

Fields:
- id
- rule_type
- pattern
- enabled
- source
- weight
- created_at

### domain_blocklist
Optimized bulk storage for imported domain blocklists. Separate from `blocked_rules` to support fast hash-based lookups at DNS query time.

Fields:
- domain_hash (SHA-256 hash of the normalized domain, primary key)
- domain (the full domain string)
- source (blocklist origin identifier)
- imported_at

Design notes:
- Indexed on `domain_hash` for O(1) lookups.
- At startup, the entire table is loaded into an in-memory `HashSet<string>` for sub-millisecond DNS lookups.
- Bulk imports use `INSERT OR IGNORE` to deduplicate.
- Estimated memory: ~30 MB for 500K domains (average 60 bytes/domain).

### integrity_checks
Stores audit results.

Fields:
- id
- timestamp
- component
- result
- details_json
- recovered
- recovery_action

### module_health
Stores current health snapshots.

Fields:
- component
- status
- last_checked_at
- detail_json

### policy_versions
Stores versioned policy packs. HMAC-protected (see §9).

Fields:
- id
- name
- version
- checksum
- created_at
- row_hmac

### install_metadata
Stores installer and version metadata.

Fields:
- install_id
- installed_at
- app_version
- service_version
- extension_version
- notes

## 4. Schema requirements

- Use migrations.
- Validate schema integrity on startup.
- Keep schema version in a dedicated table.
- Support rollback-safe upgrades.

## 5. Data retention

Concrete retention limits:

| Table | Retention policy | Max size guidance |
|---|---|---|
| events (severity: info) | 30 days | ~50K rows |
| events (severity: warning+) | 1 year | ~10K rows |
| events (severity: tamper/integrity) | Permanent | Unbounded (expected low volume) |
| integrity_checks | 90 days | ~5K rows |
| module_health | Current snapshot only (1 row per component) | ~20 rows |
| domain_blocklist | Until next blocklist refresh | ~500K rows |
| blocked_rules | Permanent (user-curated rules) | ~1K rows |

The service runs a daily maintenance job that:
1. Deletes `events` rows older than their retention period.
2. Deletes `integrity_checks` rows older than 90 days.
3. Runs `VACUUM` if more than 10K rows were deleted.
4. Total database size target: ≤ 100 MB under normal operation.

## 6. Backup strategy

- local encrypted backups where possible,
- restore from last valid snapshot,
- verify checksum before restore.

## 7. Corruption handling

If corruption is detected:
- stop trusting the database,
- attempt repair from backup,
- enter degraded mode if necessary.

## 8. Privacy
Data must remain local by default. No cloud sync unless explicitly added later.

## 9. Row integrity (HMAC)

Critical tables (`lock_state`, `policy_versions`) include a `row_hmac` column. This HMAC is computed over all other columns in the row using the HMAC key stored in DPAPI-protected key storage (see SECURITY.md §3).

On every read of a critical row:
1. Recompute the HMAC over the row's data columns.
2. Compare against the stored `row_hmac`.
3. If mismatched, log `AEGIS-8006` and treat the row as tampered.
4. Fall back to backup or enter degraded mode.

On every write to a critical row:
1. Compute the HMAC over the new data columns.
2. Store it in `row_hmac`.

This protects against direct SQLite edits by the user (e.g., modifying `expires_at` to bypass the lock timer).
