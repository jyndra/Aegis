using Aegis.Core.Interfaces;
using Aegis.Infrastructure.Commitment;
using Aegis.Infrastructure.Security;
using Aegis.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Aegis.Infrastructure.Tests;

public class CommitLockEngineTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly SqliteStorageService _storageService;
    private readonly SecurityService _securityService;
    private readonly EventRepository _eventRepo;
    private readonly Mock<ITimeProvider> _mockTimeProvider;
    private readonly CommitLockEngine _lockEngine;
    private DateTimeOffset _currentTime;

    public CommitLockEngineTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"aegis_lock_test_{Guid.NewGuid():N}.db");
        var migrator = new DatabaseMigrator(NullLogger<DatabaseMigrator>.Instance);
        _storageService = new SqliteStorageService(NullLogger<SqliteStorageService>.Instance, migrator, _tempDbPath);
        _storageService.InitializeDatabaseAsync().GetAwaiter().GetResult();

        _securityService = new SecurityService(NullLogger<SecurityService>.Instance);
        _eventRepo = new EventRepository(_storageService, NullLogger<EventRepository>.Instance);

        _currentTime = DateTimeOffset.UtcNow;
        _mockTimeProvider = new Mock<ITimeProvider>();
        _mockTimeProvider.Setup(t => t.UtcNow).Returns(() => _currentTime);

        _lockEngine = new CommitLockEngine(_storageService, _securityService, _mockTimeProvider.Object, _eventRepo, NullLogger<CommitLockEngine>.Instance);
    }

    [Fact]
    public async Task GetLockStateAsync_ReturnsState_WithValidHmac()
    {
        var state = await _lockEngine.GetLockStateAsync();
        state.Should().NotBeNull();
        state.Locked.Should().BeTrue();
    }

    [Fact]
    public async Task InitiateUnlockStageAsync_Stage1_StartsCoolingOffPeriod()
    {
        var result = await _lockEngine.InitiateUnlockStageAsync(1);

        result.Success.Should().BeTrue();
        result.CurrentStage.Should().Be(1);
        result.CooldownRemaining.Should().NotBeNull();
        result.CooldownRemaining!.Value.TotalHours.Should().Be(48);
    }

    [Fact]
    public async Task InitiateUnlockStageAsync_Stage2_RejectsPrematureCooldown()
    {
        // Stage 1
        await _lockEngine.InitiateUnlockStageAsync(1);

        // Immediate Stage 2 attempt -> should fail due to 48h cooldown
        var result = await _lockEngine.InitiateUnlockStageAsync(2, "CONFIRM_UNLOCK_AEGIS");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Cooling-off period incomplete");
    }

    [Fact]
    public async Task InitiateUnlockStageAsync_Stage2_IncrementsFailedAttemptsOnWrongPassphrase()
    {
        await _lockEngine.InitiateUnlockStageAsync(1);

        // Advance simulated time past the 48-hour cooldown
        _currentTime = _currentTime.AddHours(49);

        // Submit invalid passphrase
        var result = await _lockEngine.InitiateUnlockStageAsync(2, "WRONG_PASSPHRASE");

        result.Success.Should().BeFalse();
        var state = await _lockEngine.GetLockStateAsync();
        state.FailedAttempts.Should().BeGreaterThan(0);
    }

    public void Dispose()
    {
        try { if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath); } catch { }
    }
}
