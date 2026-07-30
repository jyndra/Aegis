using Aegis.Core.Errors;
using Aegis.Core.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Storage;

public class SqliteStorageService : IStorageService
{
    private readonly ILogger<SqliteStorageService> _logger;
    private readonly DatabaseMigrator _migrator;
    private readonly string _dbPath;
    private readonly string _connectionString;

    public SqliteStorageService(ILogger<SqliteStorageService> logger, DatabaseMigrator migrator, string? customDbPath = null)
    {
        _logger = logger;
        _migrator = migrator;

        if (!string.IsNullOrWhiteSpace(customDbPath))
        {
            _dbPath = customDbPath;
        }
        else
        {
            var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Aegis");
            _dbPath = Path.Combine(baseDir, "aegis.db");
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public string DbPath => _dbPath;

    public SqliteConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }

    public async Task InitializeDatabaseAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var dir = Path.GetDirectoryName(_dbPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                _logger.LogInformation("Created database directory at {Directory}", dir);
            }

            using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);

            // Configure PRAGMAs for WAL mode & safety
            var pragmaCmd = connection.CreateCommand();
            pragmaCmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON; PRAGMA synchronous=NORMAL;";
            await pragmaCmd.ExecuteNonQueryAsync(cancellationToken);

            // Run database migrations
            await _migrator.MigrateAsync(connection, cancellationToken);

            _logger.LogInformation("Database initialized successfully at {Path}", _dbPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize SQLite database at {Path}", _dbPath);
            throw new AegisException(AegisErrorCodes.DatabaseCorrupted, $"Database initialization failed at {_dbPath}", ex);
        }
    }

    public async Task<bool> CheckIntegrityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);

            var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA integrity_check;";
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            string status = result?.ToString() ?? "failed";

            bool ok = string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase);
            if (!ok)
            {
                _logger.LogWarning("SQLite integrity check failed for {Path}: {Status}", _dbPath, status);
            }

            return ok;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing SQLite integrity check on {Path}", _dbPath);
            return false;
        }
    }

    public async Task BackupDatabaseAsync(string backupPath, CancellationToken cancellationToken = default)
    {
        try
        {
            using var sourceConn = CreateConnection();
            await sourceConn.OpenAsync(cancellationToken);

            var destConnString = new SqliteConnectionStringBuilder
            {
                DataSource = backupPath,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString();

            using var destConn = new SqliteConnection(destConnString);
            await destConn.OpenAsync(cancellationToken);

            sourceConn.BackupDatabase(destConn);
            _logger.LogInformation("Database backup successfully written to {BackupPath}", backupPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create database backup at {BackupPath}", backupPath);
            throw new AegisException(AegisErrorCodes.DatabaseBackupFailed, $"Failed to backup database to {backupPath}", ex);
        }
    }

    public async Task RestoreDatabaseAsync(string backupPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(backupPath))
        {
            throw new AegisException(AegisErrorCodes.DatabaseRestoreFailed, $"Backup file not found at {backupPath}");
        }

        try
        {
            File.Copy(backupPath, _dbPath, overwrite: true);
            _logger.LogInformation("Database restored successfully from {BackupPath}", backupPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore database from {BackupPath}", backupPath);
            throw new AegisException(AegisErrorCodes.DatabaseRestoreFailed, $"Failed to restore database from {backupPath}", ex);
        }
    }
}
