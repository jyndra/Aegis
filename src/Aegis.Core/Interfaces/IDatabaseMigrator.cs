namespace Aegis.Core.Interfaces;

public interface IDatabaseMigrator
{
    Task MigrateAsync(CancellationToken cancellationToken = default);
    Task<int> GetCurrentVersionAsync(CancellationToken cancellationToken = default);
}
