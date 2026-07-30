using Aegis.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aegis.Infrastructure.Tests;

public class SqliteStorageServiceTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly SqliteStorageService _storageService;

    public SqliteStorageServiceTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"aegis_storage_test_{Guid.NewGuid():N}.db");
        var migrator = new DatabaseMigrator(NullLogger<DatabaseMigrator>.Instance);
        _storageService = new SqliteStorageService(NullLogger<SqliteStorageService>.Instance, migrator, _tempDbPath);
    }

    [Fact]
    public async Task InitializeDatabaseAsync_CreatesDatabaseFileAndTables()
    {
        await _storageService.InitializeDatabaseAsync();

        File.Exists(_tempDbPath).Should().BeTrue();

        bool integrityOk = await _storageService.CheckIntegrityAsync();
        integrityOk.Should().BeTrue();
    }

    [Fact]
    public async Task BackupAndRestore_CreatesValidBackup_AndRestoresSuccessfully()
    {
        await _storageService.InitializeDatabaseAsync();

        string backupPath = _tempDbPath + ".bak";

        try
        {
            await _storageService.BackupDatabaseAsync(backupPath);
            File.Exists(backupPath).Should().BeTrue();

            await _storageService.RestoreDatabaseAsync(backupPath);
            bool integrityOk = await _storageService.CheckIntegrityAsync();
            integrityOk.Should().BeTrue();
        }
        finally
        {
            if (File.Exists(backupPath))
            {
                try { File.Delete(backupPath); } catch { }
            }
        }
    }

    public void Dispose()
    {
        if (File.Exists(_tempDbPath))
        {
            try { File.Delete(_tempDbPath); } catch { }
        }
    }
}
