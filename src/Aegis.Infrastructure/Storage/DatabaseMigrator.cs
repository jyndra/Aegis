using Aegis.Core.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Storage;

public class DatabaseMigrator : IDatabaseMigrator
{
    private readonly ILogger<DatabaseMigrator> _logger;
    private Func<SqliteConnection>? _connectionFactory;

    public DatabaseMigrator(ILogger<DatabaseMigrator> logger)
    {
        _logger = logger;
    }

    public void ConfigureConnectionFactory(Func<SqliteConnection> connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        if (_connectionFactory == null)
        {
            throw new InvalidOperationException("Connection factory not configured for DatabaseMigrator.");
        }

        using var connection = _connectionFactory();
        await connection.OpenAsync(cancellationToken);
        await MigrateAsync(connection, cancellationToken);
    }

    public async Task MigrateAsync(System.Data.Common.DbConnection connection, CancellationToken cancellationToken = default)
    {
        if (connection is SqliteConnection sqliteConn)
        {
            await MigrateAsync(sqliteConn, cancellationToken);
        }
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

        await EnsureLockStateColumnsAsync(connection, cancellationToken);
    }

    public async Task<int> GetCurrentVersionAsync(CancellationToken cancellationToken = default)
    {
        if (_connectionFactory == null)
        {
            return 0;
        }

        using var connection = _connectionFactory();
        await connection.OpenAsync(cancellationToken);
        return await GetCurrentVersionAsync(connection, cancellationToken);
    }

    public async Task<int> GetCurrentVersionAsync(System.Data.Common.DbConnection connection, CancellationToken cancellationToken = default)
    {
        if (connection is SqliteConnection sqliteConn)
        {
            return await GetCurrentVersionAsync(sqliteConn, cancellationToken);
        }
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
                locked INTEGER NOT NULL DEFAULT 1,
                lock_started_at TEXT NOT NULL,
                lock_expires_at TEXT NOT NULL,
                unlock_requested_at TEXT,
                stage INTEGER NOT NULL DEFAULT 0,
                failed_attempts INTEGER NOT NULL DEFAULT 0,
                updated_at TEXT NOT NULL,
                hmac_signature TEXT
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
                check_type TEXT NOT NULL,
                status TEXT NOT NULL,
                details_json TEXT,
                checked_at TEXT NOT NULL
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

    private static async Task EnsureLockStateColumnsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var checkCmd = connection.CreateCommand();
        checkCmd.CommandText = "PRAGMA table_info(lock_state);";

        bool hasLockedCol = false;
        using (var colReader = await checkCmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await colReader.ReadAsync(cancellationToken))
            {
                string colName = colReader.GetString(1);
                if (string.Equals(colName, "locked", StringComparison.OrdinalIgnoreCase))
                {
                    hasLockedCol = true;
                    break;
                }
            }
        }

        if (!hasLockedCol)
        {
            var dropCmd = connection.CreateCommand();
            dropCmd.CommandText = @"
                DROP TABLE IF EXISTS lock_state;
                CREATE TABLE lock_state (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    locked INTEGER NOT NULL DEFAULT 1,
                    lock_started_at TEXT NOT NULL,
                    lock_expires_at TEXT NOT NULL,
                    unlock_requested_at TEXT,
                    stage INTEGER NOT NULL DEFAULT 0,
                    failed_attempts INTEGER NOT NULL DEFAULT 0,
                    updated_at TEXT NOT NULL,
                    hmac_signature TEXT
                );";
            await dropCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        var icCheckCmd = connection.CreateCommand();
        icCheckCmd.CommandText = "PRAGMA table_info(integrity_checks);";
        bool hasCheckTypeCol = false;
        using (var icReader = await icCheckCmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await icReader.ReadAsync(cancellationToken))
            {
                string colName = icReader.GetString(1);
                if (string.Equals(colName, "check_type", StringComparison.OrdinalIgnoreCase))
                {
                    hasCheckTypeCol = true;
                    break;
                }
            }
        }

        if (!hasCheckTypeCol)
        {
            var dropIcCmd = connection.CreateCommand();
            dropIcCmd.CommandText = @"
                DROP TABLE IF EXISTS integrity_checks;
                CREATE TABLE integrity_checks (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    check_type TEXT NOT NULL,
                    status TEXT NOT NULL,
                    details_json TEXT,
                    checked_at TEXT NOT NULL
                );";
            await dropIcCmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
