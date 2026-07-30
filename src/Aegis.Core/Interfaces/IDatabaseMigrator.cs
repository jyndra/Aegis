using Microsoft.Data.Sqlite;

namespace Aegis.Core.Interfaces;

public interface IDatabaseMigrator
{
    Task MigrateAsync(CancellationToken cancellationToken = default);
    Task MigrateAsync(SqliteConnection connection, CancellationToken cancellationToken = default);
    Task<int> GetCurrentVersionAsync(CancellationToken cancellationToken = default);
    Task<int> GetCurrentVersionAsync(SqliteConnection connection, CancellationToken cancellationToken = default);
}
