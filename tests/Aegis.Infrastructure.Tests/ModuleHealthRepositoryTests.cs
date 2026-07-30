using Aegis.Core.Models;
using Aegis.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aegis.Infrastructure.Tests;

public class ModuleHealthRepositoryTests : IDisposable
{
    private readonly SqliteStorageService _storageService;
    private readonly ModuleHealthRepository _healthRepo;

    public ModuleHealthRepositoryTests()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"aegis_health_test_{Guid.NewGuid():N}.db");
        var migrator = new DatabaseMigrator(NullLogger<DatabaseMigrator>.Instance);
        _storageService = new SqliteStorageService(NullLogger<SqliteStorageService>.Instance, migrator, tempDbPath);
        _storageService.InitializeDatabaseAsync().GetAwaiter().GetResult();

        _healthRepo = new ModuleHealthRepository(_storageService, NullLogger<ModuleHealthRepository>.Instance);
    }

    [Fact]
    public async Task SaveHealthReportAsync_PerformsUpsertOnConflict()
    {
        var now = DateTimeOffset.UtcNow;
        var initial = new HealthReport("DNS", "Healthy", now, "{\"entries\": 100}");
        var updated = new HealthReport("DNS", "Degraded", now.AddMinutes(5), "{\"entries\": 0}");

        await _healthRepo.SaveHealthReportAsync(initial);
        await _healthRepo.SaveHealthReportAsync(updated);

        var reports = await _healthRepo.GetAllHealthReportsAsync();
        reports.Should().HaveCount(1);

        var report = reports.Single();
        report.Component.Should().Be("DNS");
        report.Status.Should().Be("Degraded");
        report.DetailJson.Should().Contain("entries\": 0");
    }

    public void Dispose()
    {
        if (File.Exists(_storageService.DbPath))
        {
            try { File.Delete(_storageService.DbPath); } catch { }
        }
    }
}
