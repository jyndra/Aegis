using System.Data.Common;

namespace Aegis.Core.Interfaces;

public interface IDatabaseMigrator
{
    Task MigrateAsync(CancellationToken cancellationToken = default);
    Task MigrateAsync(DbConnection connection, CancellationToken cancellationToken = default);
    Task<int> GetCurrentVersionAsync(CancellationToken cancellationToken = default);
    Task<int> GetCurrentVersionAsync(DbConnection connection, CancellationToken cancellationToken = default);
}
