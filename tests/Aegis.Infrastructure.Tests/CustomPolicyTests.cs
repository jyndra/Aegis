using System.IO;
using Aegis.Core.Configuration;
using Aegis.Core.Interfaces;
using Aegis.Core.Models;
using Aegis.Infrastructure.Deployment;
using Aegis.Infrastructure.Rules;
using Aegis.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Aegis.Infrastructure.Tests;

public class CustomPolicyTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteStorageService _storageService;
    private readonly Mock<IBlocklistRepository> _mockBlocklist = new();
    private readonly Mock<IDnsFilter> _mockDns = new();
    private readonly Mock<IRegexEngine> _mockRegex = new();
    private readonly Mock<IKeywordEngine> _mockKeyword = new();
    private readonly Mock<ICommitLockEngine> _mockCommitLock = new();

    public CustomPolicyTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), "Aegis_CustomPolicyTest_" + Guid.NewGuid().ToString("N")[..8] + ".db");
        var migrator = new DatabaseMigrator(NullLogger<DatabaseMigrator>.Instance);
        _storageService = new SqliteStorageService(NullLogger<SqliteStorageService>.Instance, migrator, _testDbPath);
        _storageService.InitializeDatabaseAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task AddCustomWebsiteAsync_InvokesBlocklistRepoAndHotReloadsDnsFilter()
    {
        var options = Options.Create(new LockOptions { BypassLockForTesting = true });
        var service = new CustomPolicyService(
            _mockBlocklist.Object, _mockDns.Object, _mockRegex.Object, _mockKeyword.Object,
            _mockCommitLock.Object, _storageService, options, NullLogger<CustomPolicyService>.Instance);

        bool result = await service.AddCustomWebsiteAsync("custom-blocked-site.com");

        result.Should().BeTrue();
        _mockBlocklist.Verify(b => b.AddDomainAsync("custom-blocked-site.com", "UserCustom", It.IsAny<CancellationToken>()), Times.Once);
        _mockDns.Verify(d => d.ReloadBlocklistAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddCustomKeywordAsync_PersistsBlockedRuleAndHotReloadsKeywordEngine()
    {
        var options = Options.Create(new LockOptions { BypassLockForTesting = true });
        var service = new CustomPolicyService(
            _mockBlocklist.Object, _mockDns.Object, _mockRegex.Object, _mockKeyword.Object,
            _mockCommitLock.Object, _storageService, options, NullLogger<CustomPolicyService>.Instance);

        bool result = await service.AddCustomKeywordAsync("casino", 65);

        result.Should().BeTrue();
        _mockKeyword.Verify(k => k.ReloadKeywordsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddCustomRegexAsync_WhenValidPattern_PersistsAndHotReloadsRegexEngine()
    {
        var options = Options.Create(new LockOptions { BypassLockForTesting = true });
        var service = new CustomPolicyService(
            _mockBlocklist.Object, _mockDns.Object, _mockRegex.Object, _mockKeyword.Object,
            _mockCommitLock.Object, _storageService, options, NullLogger<CustomPolicyService>.Instance);

        bool result = await service.AddCustomRegexAsync(@"\b(poker|roulette)\b", 70, "Betting regex");

        result.Should().BeTrue();
        _mockRegex.Verify(r => r.ReloadPatternsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddCustomRegexAsync_WhenInvalidPattern_ThrowsArgumentException()
    {
        var options = Options.Create(new LockOptions { BypassLockForTesting = true });
        var service = new CustomPolicyService(
            _mockBlocklist.Object, _mockDns.Object, _mockRegex.Object, _mockKeyword.Object,
            _mockCommitLock.Object, _storageService, options, NullLogger<CustomPolicyService>.Instance);

        var act = async () => await service.AddCustomRegexAsync(@"[invalid-regex-pattern(", 50);

        await act.Should().ThrowAsync<ArgumentException>()
                 .WithMessage("*Invalid regular expression pattern*");
    }

    [Fact]
    public async Task RemoveCustomRuleAsync_WhenLockIsActiveAndBypassIsFalse_StrictlyRejectsDeletion()
    {
        // Enforce active commitment lock WITHOUT test mode bypass
        var lockStatus = new CommitLockStatus(
            Locked: true,
            Stage: UnlockStage.Locked,
            StageChangedAt: DateTimeOffset.UtcNow,
            LockExpiresAt: DateTimeOffset.UtcNow.AddDays(25),
            CanAdvance: false,
            SecondsRemainingInStage: 86400 * 25,
            NextStageAvailableAt: null
        );
        _mockCommitLock.Setup(c => c.GetStatusAsync(It.IsAny<CancellationToken>()))
                       .ReturnsAsync(lockStatus);

        var options = Options.Create(new LockOptions { BypassLockForTesting = false }); // Production locked mode
        var service = new CustomPolicyService(
            _mockBlocklist.Object, _mockDns.Object, _mockRegex.Object, _mockKeyword.Object,
            _mockCommitLock.Object, _storageService, options, NullLogger<CustomPolicyService>.Instance);

        var (success, message) = await service.RemoveCustomRuleAsync(1);

        success.Should().BeFalse();
        message.Should().Contain("Protection Ratchet Active");
    }

    [Fact]
    public async Task RemoveCustomRuleAsync_WhenTestModeActive_AllowsRuleDeletion()
    {
        var lockStatus = new CommitLockStatus(
            Locked: true,
            Stage: UnlockStage.Locked,
            StageChangedAt: DateTimeOffset.UtcNow,
            LockExpiresAt: DateTimeOffset.UtcNow.AddDays(25),
            CanAdvance: false,
            SecondsRemainingInStage: 86400 * 25,
            NextStageAvailableAt: null
        );
        _mockCommitLock.Setup(c => c.GetStatusAsync(It.IsAny<CancellationToken>()))
                       .ReturnsAsync(lockStatus);

        var options = Options.Create(new LockOptions { BypassLockForTesting = true }); // Test mode bypass
        var service = new CustomPolicyService(
            _mockBlocklist.Object, _mockDns.Object, _mockRegex.Object, _mockKeyword.Object,
            _mockCommitLock.Object, _storageService, options, NullLogger<CustomPolicyService>.Instance);

        // Add a rule to delete
        await service.AddCustomKeywordAsync("testkeyword");

        var (success, message) = await service.RemoveCustomRuleAsync(1);

        success.Should().BeTrue();
        message.Should().Contain("removed");
    }

    [Fact]
    public async Task Uninstaller_WhenTestModeActive_AuthorizesUninstallation()
    {
        var lockStatus = new CommitLockStatus(
            Locked: true,
            Stage: UnlockStage.Locked,
            StageChangedAt: DateTimeOffset.UtcNow,
            LockExpiresAt: DateTimeOffset.UtcNow.AddDays(25),
            CanAdvance: false,
            SecondsRemainingInStage: 86400 * 25,
            NextStageAvailableAt: null
        );
        _mockCommitLock.Setup(c => c.GetStatusAsync(It.IsAny<CancellationToken>()))
                       .ReturnsAsync(lockStatus);

        var options = Options.Create(new LockOptions { BypassLockForTesting = true });
        var uninstaller = new UninstallerService(_mockCommitLock.Object, options);

        var (canUninstall, reason) = await uninstaller.CheckCanUninstallAsync();

        canUninstall.Should().BeTrue();
        reason.Should().Contain("Test mode active");
    }

    [Fact]
    public async Task Uninstaller_WhenTestModeActive_AllowsZeroFrictionUninstallation()
    {
        var options = Options.Create(new LockOptions { BypassLockForTesting = true });
        var uninstaller = new UninstallerService(_mockCommitLock.Object, options);

        // In test mode (BypassLockForTesting = true), uninstallation executes cleanly with zero friction
        var result = await uninstaller.UninstallAsync(forceConfirm: true);

        result.Success.Should().BeTrue("Zero friction uninstallation must be authorized in test mode");
    }

    [Fact]
    public async Task Uninstaller_InProductionMode_EnforcesTenStepChallengeAndFiveMinuteCooldownPerStep()
    {
        var unlockedStatus = new CommitLockStatus(
            Locked: false,
            Stage: UnlockStage.Unlocked,
            StageChangedAt: DateTimeOffset.UtcNow,
            LockExpiresAt: DateTimeOffset.UtcNow.AddMinutes(-5),
            CanAdvance: true,
            SecondsRemainingInStage: 0,
            NextStageAvailableAt: null
        );
        _mockCommitLock.Setup(c => c.GetStatusAsync(It.IsAny<CancellationToken>()))
                       .ReturnsAsync(unlockedStatus);

        var options = Options.Create(new LockOptions { BypassLockForTesting = false }); // Production mode
        var uninstaller = new UninstallerService(_mockCommitLock.Object, options);

        // Step 1 confirmation
        var step1Result = await uninstaller.UninstallAsync(forceConfirm: true, confirmationStep: 1);
        step1Result.Success.Should().BeFalse();
        step1Result.Message.Should().Contain("Step 1/10 confirmed!");

        // Immediate Step 2 submission should be rejected due to 5-minute cooldown
        var step2ImmediateResult = await uninstaller.UninstallAsync(forceConfirm: true, confirmationStep: 2);
        step2ImmediateResult.Success.Should().BeFalse();
        step2ImmediateResult.Message.Should().Contain("Step 2 Cooldown Active");
        step2ImmediateResult.Message.Should().Contain("Mandatory 5-minute cooling-off period");
    }

    public void Dispose()
    {
        try { if (File.Exists(_testDbPath)) File.Delete(_testDbPath); } catch { }
    }
}
