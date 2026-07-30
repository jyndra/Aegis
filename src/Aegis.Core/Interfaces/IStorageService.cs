namespace Aegis.Core.Interfaces;

/// <summary>
/// Provides core SQLite database connection and backup/restore management.
/// </summary>
public interface IStorageService
{
    Task InitializeDatabaseAsync(CancellationToken cancellationToken = default);
    Task BackupDatabaseAsync(string backupPath, CancellationToken cancellationToken = default);
    Task RestoreDatabaseAsync(string backupPath, CancellationToken cancellationToken = default);
    Task<bool> CheckIntegrityAsync(CancellationToken cancellationToken = default);
}
