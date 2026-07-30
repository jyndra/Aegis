using Aegis.Core.Interfaces;

namespace Aegis.Infrastructure.Storage;

internal class SqliteStorageService : IStorageService
{
    public Task InitializeDatabaseAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task BackupDatabaseAsync(string backupPath, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task RestoreDatabaseAsync(string backupPath, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<bool> CheckIntegrityAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
