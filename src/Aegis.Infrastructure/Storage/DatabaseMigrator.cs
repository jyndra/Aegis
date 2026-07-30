using Aegis.Core.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Storage;

public class DatabaseMigrator : IDatabaseMigrator
{
    private readonly ILogger<DatabaseMigrator> _logger;

    public DatabaseMigrator(ILogger<DatabaseMigrator> logger)
    {
        _logger = logger;
    }

    public async Task MigrateAsync(SqliteConnection connection, CancellationToken cancellationToken = default)
    {
        int currentVersion = await GetCurrentVersionAsync(connection, cancellationToken);
        _logger.LogInformation("Current database schema version: {Version}", currentVersion);

        if (currentVersion < 1)
        {
            _logger.LogInformation("Applying database migration v1...");
            await ApplyV1SchemaAsync(connection, cancellationToken);
            _logger.LogInformation("Database migration v1 applied successfully.");
        }
    }

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Use MigrateAsync(SqliteConnection) for migration execution.");
    }

    public async Task<int> GetCurrentVersionAsync(CancellationToken cancellationToken = default)
    {
        return 0;
    }

    public async Task<int> GetCurrentVersionAsync(SqliteConnection connection, CancellationToken cancellationToken = default)
    {
        var checkCmd = connection.CreateCommand();
        checkCmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='schema_version';";
        var result = await checkCmd.ExecuteScalarAsync(cancellationToken);
        long count = result is long l ? l : 0;

        if (count == 0)
        {
            return 0;
        }

        var versionCmd = connection.CreateCommand();
        versionCmd.CommandText = "SELECT MAX(version) FROM schema_version;";
        var versionResult = await versionCmd.ExecuteScalarAsync(cancellationToken);
        if (versionResult == null || versionResult is DBNull)
        {
            return 0;
        }

        return Convert.ToInt32(versionResult);
    }

    private static async Task ApplyV1SchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        using var transaction = connection.BeginTransaction();

        var sql = @"
            CREATE TABLE IF NOT EXISTS schema_version (
                version INTEGER PRIMARY KEY,
                applied_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS lock_state (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                is_locked INTEGER NOT NULL DEFAULT 1,
                activated_at TEXT NOT NULL,
                expires_at TEXT NOT NULL,
                activated_monotonic_ticks INTEGER NOT NULL,
                elapsed_monotonic_ticks INTEGER NOT NULL,
                last_tick_update_at TEXT NOT NULL,
                unlock_requested_at TEXT,
                unlock_stage INTEGER NOT NULL DEFAULT 0,
                unlock_state TEXT NOT NULL DEFAULT 'Locked',
                last_change_at TEXT NOT NULL,
                row_hmac TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS events (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp TEXT NOT NULL,
                component TEXT NOT NULL,
                event_type TEXT NOT NULL,
                severity TEXT NOT NULL,
                message TEXT NOT NULL,
                details_json TEXT
            );

            CREATE TABLE IF NOT EXISTS blocked_rules (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                rule_type TEXT NOT NULL,
                pattern TEXT NOT NULL,
                enabled INTEGER NOT NULL DEFAULT 1,
                source TEXT NOT NULL,
                weight INTEGER NOT NULL DEFAULT 1,
                created_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS domain_blocklist (
                domain_hash TEXT PRIMARY KEY,
                domain TEXT NOT NULL,
                source TEXT NOT NULL,
                imported_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS integrity_checks (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp TEXT NOT NULL,
                component TEXT NOT NULL,
                passed INTEGER NOT NULL,
                details_json TEXT,
                recovered INTEGER NOT NULL DEFAULT 0,
                recovery_action TEXT
            );

            CREATE TABLE IF NOT EXISTS module_health (
                component TEXT PRIMARY KEY,
                status TEXT NOT NULL,
                last_checked_at TEXT NOT NULL,
                detail_json TEXT
            );

            CREATE TABLE IF NOT EXISTS policy_versions (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                version TEXT NOT NULL,
                checksum TEXT NOT NULL,
                created_at TEXT NOT NULL,
                row_hmac TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS install_metadata (
                install_id TEXT PRIMARY KEY,
                installed_at TEXT NOT NULL,
                app_version TEXT NOT NULL,
                service_version TEXT NOT NULL,
                extension_version TEXT NOT NULL,
                notes TEXT
            );

            INSERT INTO schema_version (version, applied_at) VALUES (1, datetime('now'));
        ";

        var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        transaction.Commit();
    }
}
