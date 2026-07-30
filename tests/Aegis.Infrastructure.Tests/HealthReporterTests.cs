using Aegis.Core.Models;
using Aegis.Infrastructure.Health;
using Aegis.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aegis.Infrastructure.Tests;

public class HealthReporterTests : IDisposable
{
    private readonly SqliteStorageService _storageService;
    private readonly ModuleHealthRepository _healthRepo;
    private readonly HealthReporter _healthReporter;

    public HealthReporterTests()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"aegis_test_{Guid.NewGuid():N}.db");
        var migrator = new DatabaseMigrator(NullLogger<DatabaseMigrator>.Instance);
        _storageService = new SqliteStorageService(NullLogger<SqliteStorageService>.Instance, migrator, tempDbPath);
        _storageService.InitializeDatabaseAsync().GetAwaiter().GetResult();

        _healthRepo = new ModuleHealthRepository(_storageService, NullLogger<ModuleHealthRepository>.Instance);
        var timeProvider = new Aegis.Infrastructure.Time.SystemTimeProvider();
        _healthReporter = new HealthReporter(_healthRepo, timeProvider, NullLogger<HealthReporter>.Instance);
    }

    [Fact]
    public async Task GetStatusReportAsync_ReturnsBaselineReports_WhenDatabaseIsEmpty()
    {
        var reports = await _healthReporter.GetStatusReportAsync();
        reports.Should().NotBeEmpty();
        reports.Should().Contain(r => r.Component == "Service");
    }

    [Fact]
    public async Task RecordHealthAsync_SavesHealthReportToDatabase()
    {
        await _healthReporter.RecordHealthAsync("DNS", "Healthy", "{\"listenPort\": 53}");

        var reports = await _healthReporter.GetStatusReportAsync();
        var dnsReport = reports.FirstOrDefault(r => r.Component == "DNS");

        dnsReport.Should().NotBeNull();
        dnsReport!.Status.Should().Be("Healthy");
        dnsReport.DetailJson.Should().Contain("listenPort");
    }

    public void Dispose()
    {
        if (File.Exists(_storageService.DbPath))
        {
            try { File.Delete(_storageService.DbPath); } catch { }
        }
    }
}
