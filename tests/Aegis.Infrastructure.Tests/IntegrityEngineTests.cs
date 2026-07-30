using Aegis.Infrastructure.Health;
using Aegis.Infrastructure.Integrity;
using Aegis.Infrastructure.Security;
using Aegis.Infrastructure.Storage;
using Aegis.Infrastructure.Time;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aegis.Infrastructure.Tests;

public class IntegrityEngineTests : IDisposable
{
    private readonly SqliteStorageService _storageService;
    private readonly SecurityService _securityService;
    private readonly ModuleHealthRepository _healthRepo;
    private readonly HealthReporter _healthReporter;
    private readonly EventRepository _eventRepo;
    private readonly IntegrityEngine _integrityEngine;

    public IntegrityEngineTests()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"aegis_integrity_test_{Guid.NewGuid():N}.db");
        var migrator = new DatabaseMigrator(NullLogger<DatabaseMigrator>.Instance);
        _storageService = new SqliteStorageService(NullLogger<SqliteStorageService>.Instance, migrator, tempDbPath);
        _storageService.InitializeDatabaseAsync().GetAwaiter().GetResult();

        _securityService = new SecurityService(NullLogger<SecurityService>.Instance);
        _healthRepo = new ModuleHealthRepository(_storageService, NullLogger<ModuleHealthRepository>.Instance);
        _healthReporter = new HealthReporter(_healthRepo, NullLogger<HealthReporter>.Instance);
        _eventRepo = new EventRepository(_storageService, NullLogger<EventRepository>.Instance);
        var timeProvider = new SystemTimeProvider();

        _integrityEngine = new IntegrityEngine(_storageService, _securityService, _healthReporter, _eventRepo, timeProvider, NullLogger<IntegrityEngine>.Instance);
    }

    [Fact]
    public async Task RunBootAuditAsync_ReturnsReport_WithIntegrityChecks()
    {
        var report = await _integrityEngine.RunBootAuditAsync();

        report.Should().NotBeNull();
        report.Checks.Should().NotBeEmpty();
        report.Checks.Should().Contain(c => c.CheckType == "SqliteIntegrity");
        report.Checks.Should().Contain(c => c.CheckType == "LockStateHmac");
    }

    [Fact]
    public async Task RunPeriodicAuditAsync_ReturnsHealthyReport()
    {
        var report = await _integrityEngine.RunPeriodicAuditAsync();

        report.Should().NotBeNull();
        report.Healthy.Should().BeTrue();
    }

    [Fact]
    public async Task AttemptSelfHealingAsync_RestoresMissingPolicyFiles()
    {
        bool healed = await _integrityEngine.AttemptSelfHealingAsync("Policy");

        healed.Should().BeTrue();

        string policyDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Aegis", "policies");
        File.Exists(Path.Combine(policyDir, "keywords-default.json")).Should().BeTrue();
        File.Exists(Path.Combine(policyDir, "regex-default.json")).Should().BeTrue();
    }

    public void Dispose()
    {
        if (File.Exists(_storageService.DbPath))
        {
            try { File.Delete(_storageService.DbPath); } catch { }
        }
    }
}
