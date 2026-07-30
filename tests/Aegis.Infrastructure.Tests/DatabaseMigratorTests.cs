using Aegis.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aegis.Infrastructure.Tests;

public class DatabaseMigratorTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DatabaseMigrator _migrator;

    public DatabaseMigratorTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _migrator = new DatabaseMigrator(NullLogger<DatabaseMigrator>.Instance);
    }

    [Fact]
    public async Task GetCurrentVersionAsync_ReturnsZero_WhenDatabaseIsEmpty()
    {
        int version = await _migrator.GetCurrentVersionAsync(_connection);
        version.Should().Be(0);
    }

    [Fact]
    public async Task MigrateAsync_CreatesV1Schema_AndUpdatesVersionTo1()
    {
        await _migrator.MigrateAsync(_connection);

        int version = await _migrator.GetCurrentVersionAsync(_connection);
        version.Should().Be(1);

        // Verify key tables exist
        var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name IN ('settings', 'lock_state', 'events', 'module_health');";
        long tableCount = (long)(await cmd.ExecuteScalarAsync())!;
        tableCount.Should().Be(4);
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }
}
