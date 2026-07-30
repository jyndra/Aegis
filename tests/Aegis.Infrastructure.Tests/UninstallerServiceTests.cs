using System.IO;
using Aegis.Core.Interfaces;
using Aegis.Core.Models;
using Aegis.Infrastructure.Deployment;
using FluentAssertions;
using Moq;
using Xunit;

namespace Aegis.Infrastructure.Tests;

public class UninstallerServiceTests : IDisposable
{
    private readonly Mock<ICommitLockEngine> _mockCommitEngine = new();
    private readonly UninstallerService _uninstaller;
    private readonly string _testRoot;

    public UninstallerServiceTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "Aegis_UninstallTest_" + Guid.NewGuid().ToString("N")[..8]);
        _uninstaller = new UninstallerService(_mockCommitEngine.Object);
    }

    [Fact]
    public async Task CheckCanUninstallAsync_WhenCommitmentLockIsActive_StrictlyRejectsUninstallation()
    {
        var lockedStatus = new CommitLockStatus(
            Locked: true,
            Stage: UnlockStage.Locked,
            StageChangedAt: DateTimeOffset.UtcNow,
            LockExpiresAt: DateTimeOffset.UtcNow.AddDays(25),
            CanAdvance: false,
            SecondsRemainingInStage: 86400 * 25,
            NextStageAvailableAt: null
        );

        _mockCommitEngine.Setup(c => c.GetStatusAsync(It.IsAny<CancellationToken>()))
                         .ReturnsAsync(lockedStatus);

        var (canUninstall, reason) = await _uninstaller.CheckCanUninstallAsync();

        canUninstall.Should().BeFalse();
        reason.Should().Contain("Active 25-day commitment device lock is running");
    }

    [Fact]
    public async Task UninstallAsync_WhenCommitmentLockIsActive_RefusesRemovalAndDeletesZeroFiles()
    {
        var lockedStatus = new CommitLockStatus(
            Locked: true,
            Stage: UnlockStage.Locked,
            StageChangedAt: DateTimeOffset.UtcNow,
            LockExpiresAt: DateTimeOffset.UtcNow.AddDays(20),
            CanAdvance: false,
            SecondsRemainingInStage: 86400 * 20,
            NextStageAvailableAt: null
        );

        _mockCommitEngine.Setup(c => c.GetStatusAsync(It.IsAny<CancellationToken>()))
                         .ReturnsAsync(lockedStatus);

        // Create a fake deployed policy file
        Directory.CreateDirectory(Path.Combine(_testRoot, "policies"));
        string dummyFile = Path.Combine(_testRoot, "policies", "regex-default.json");
        await File.WriteAllTextAsync(dummyFile, "{ \"rule\": \"test\" }");

        var result = await _uninstaller.UninstallAsync(_testRoot, forceConfirm: true);

        result.Success.Should().BeFalse();
        result.BlockedByCommitmentDevice.Should().BeTrue();
        result.RemovedFiles.Should().BeEmpty();

        // Ensure file was NOT touched or deleted
        File.Exists(dummyFile).Should().BeTrue();
    }

    [Fact]
    public async Task UninstallAsync_WhenUnlocked_CleanlyReversesDeploymentAndRemovesPolicyFiles()
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

        _mockCommitEngine.Setup(c => c.GetStatusAsync(It.IsAny<CancellationToken>()))
                         .ReturnsAsync(unlockedStatus);

        Directory.CreateDirectory(Path.Combine(_testRoot, "policies"));
        string dummyFile = Path.Combine(_testRoot, "policies", "regex-default.json");
        await File.WriteAllTextAsync(dummyFile, "{ \"rule\": \"test\" }");

        var result = await _uninstaller.UninstallAsync(_testRoot, forceConfirm: true);

        result.Success.Should().BeTrue();
        result.BlockedByCommitmentDevice.Should().BeFalse();
        result.RemovedFiles.Should().Contain(dummyFile);

        File.Exists(dummyFile).Should().BeFalse();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, true);
            }
        }
        catch { }
    }
}
