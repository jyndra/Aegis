using Aegis.Core.Interfaces;

namespace Aegis.Infrastructure.Storage;

internal class DatabaseMigrator : IDatabaseMigrator
{
    public Task MigrateAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<int> GetCurrentVersionAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
