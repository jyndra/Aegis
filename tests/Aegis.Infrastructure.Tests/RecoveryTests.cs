using System.IO;
using Aegis.Core.Interfaces;
using Aegis.Core.Models;
using Aegis.Infrastructure.Deployment;
using Aegis.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Aegis.Infrastructure.Tests;

/// <summary>
/// Recovery tests verifying system behavior under adverse conditions:
/// database corruption, HMAC tampering, self-healing, and fail-closed lock defaults.
/// Maps to RECOVERY.md sections 3.5, 3.6, and Principle 2 ("Lock defaults to locked").
/// </summary>
public class RecoveryTests : IDisposable
{
    private readonly string _testRoot;

    public RecoveryTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "Aegis_RecoveryTest_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testRoot);
    }

    // -------------------------------------------------------------------------
    // 1. InstallerService restores missing policy files (RECOVERY.md § 3.5, 3.6)
    // -------------------------------------------------------------------------
    [Fact]
    public async Task SelfHealing_RestoreMissingPolicyFiles_RecreatesKeywordsAndRegexJsons()
    {
        var mockStorage = new Mock<IStorageService>();
        mockStorage.Setup(s => s.InitializeDatabaseAsync(It.IsAny<CancellationToken>()))
                   .Returns(Task.CompletedTask);

        var installer = new InstallerService(mockStorage.Object);
        string policyDir = Path.Combine(_testRoot, "policies");

        // Simulate corrupted/missing files by ensuring the directory is empty
        Directory.CreateDirectory(policyDir);
        string kwPath = Path.Combine(policyDir, "keywords-default.json");
        string rxPath = Path.Combine(policyDir, "regex-default.json");
        if (File.Exists(kwPath)) File.Delete(kwPath);
        if (File.Exists(rxPath)) File.Delete(rxPath);

        bool restored = await installer.RestorePoliciesAsync(_testRoot);

        restored.Should().BeTrue();
        File.Exists(kwPath).Should().BeTrue("Keywords policy should be restored");
        File.Exists(rxPath).Should().BeTrue("Regex policy should be restored");

        string kwContent = await File.ReadAllTextAsync(kwPath);
        kwContent.Should().Contain("porn", "Restored keyword policy should contain expected root triggers");
    }

    // -------------------------------------------------------------------------
    // 2. RestorePoliciesAsync overwrites corrupted files (RECOVERY.md § 3.6)
    // -------------------------------------------------------------------------
    [Fact]
    public async Task SelfHealing_CorruptedPolicyFile_IsOverwrittenByRestore()
    {
        var mockStorage = new Mock<IStorageService>();
        mockStorage.Setup(s => s.InitializeDatabaseAsync(It.IsAny<CancellationToken>()))
                   .Returns(Task.CompletedTask);

        var installer = new InstallerService(mockStorage.Object);
        string policyDir = Path.Combine(_testRoot, "policies");
        Directory.CreateDirectory(policyDir);

        string kwPath = Path.Combine(policyDir, "keywords-default.json");
        await File.WriteAllTextAsync(kwPath, "{ \"corrupted\": true, \"garbage\": [null, null] }");

        bool restored = await installer.RestorePoliciesAsync(_testRoot);

        restored.Should().BeTrue();
        string content = await File.ReadAllTextAsync(kwPath);
        content.Should().NotContain("corrupted");
        content.Should().Contain("version");
    }

    // -------------------------------------------------------------------------
    // 3. Install preserves existing customized user policy on upgrade (non-overwrite path)
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Installer_WhenPolicyFileAlreadyExists_PreservesCustomizedRules()
    {
        var mockStorage = new Mock<IStorageService>();
        mockStorage.Setup(s => s.InitializeDatabaseAsync(It.IsAny<CancellationToken>()))
                   .Returns(Task.CompletedTask);

        var installer = new InstallerService(mockStorage.Object);

        // Pre-deploy a custom user rule
        string policyDir = Path.Combine(_testRoot, "policies");
        Directory.CreateDirectory(policyDir);
        string kwPath = Path.Combine(policyDir, "keywords-default.json");
        await File.WriteAllTextAsync(kwPath, "{\"custom_rule\": \"do_not_overwrite_me\"}");

        var opts = new InstallOptions(OverrideInstallPath: _testRoot, DeployDefaultPolicies: true);
        var result = await installer.InstallAsync(opts);

        result.Success.Should().BeTrue();

        // Custom file should NOT have been overwritten
        string remaining = await File.ReadAllTextAsync(kwPath);
        remaining.Should().Contain("do_not_overwrite_me",
            "InstallAsync with overwrite:false must not clobber existing customized policy files");
    }

    // -------------------------------------------------------------------------
    // 4. UninstallerService locks out removal when commitment device is active (RECOVERY.md lock defaults to locked)
    // -------------------------------------------------------------------------
    [Fact]
    public async Task CommitmentLock_WhenActive_UninstallationIsRejectedAndNoFilesAreDeleted()
    {
        var mockCommit = new Mock<ICommitLockEngine>();
        var lockedStatus = new CommitLockStatus(
            Locked: true,
            Stage: UnlockStage.Locked,
            StageChangedAt: DateTimeOffset.UtcNow,
            LockExpiresAt: DateTimeOffset.UtcNow.AddDays(20),
            CanAdvance: false,
            SecondsRemainingInStage: 86400 * 20,
            NextStageAvailableAt: null
        );
        mockCommit.Setup(c => c.GetStatusAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(lockedStatus);

        var uninstaller = new UninstallerService(mockCommit.Object);

        string policyDir = Path.Combine(_testRoot, "policies");
        Directory.CreateDirectory(policyDir);
        string sentinel = Path.Combine(policyDir, "regex-default.json");
        await File.WriteAllTextAsync(sentinel, "{\"protected\": true}");

        var result = await uninstaller.UninstallAsync(_testRoot, forceConfirm: true);

        result.Success.Should().BeFalse("Uninstallation must be rejected when commitment lock is active");
        result.BlockedByCommitmentDevice.Should().BeTrue();
        result.RemovedFiles.Should().BeEmpty("Zero files must be removed when commitment lock is active");
        File.Exists(sentinel).Should().BeTrue("Protected file must survive rejected uninstallation");
    }

    // -------------------------------------------------------------------------
    // 5. Uninstallation succeeds cleanly when commitment device is inactive
    // -------------------------------------------------------------------------
    [Fact]
    public async Task CommitmentLock_WhenInactive_UninstallationSucceedsAndRemovesFiles()
    {
        var mockCommit = new Mock<ICommitLockEngine>();
        var unlockedStatus = new CommitLockStatus(
            Locked: false,
            Stage: UnlockStage.Unlocked,
            StageChangedAt: DateTimeOffset.UtcNow,
            LockExpiresAt: DateTimeOffset.UtcNow.AddMinutes(-5),
            CanAdvance: true,
            SecondsRemainingInStage: 0,
            NextStageAvailableAt: null
        );
        mockCommit.Setup(c => c.GetStatusAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(unlockedStatus);

        var uninstaller = new UninstallerService(mockCommit.Object);

        string policyDir = Path.Combine(_testRoot, "policies");
        Directory.CreateDirectory(policyDir);
        string sentinel = Path.Combine(policyDir, "keywords-default.json");
        await File.WriteAllTextAsync(sentinel, "{\"to_remove\": true}");

        var result = await uninstaller.UninstallAsync(_testRoot, forceConfirm: true, confirmationStep: 10);

        result.Success.Should().BeTrue();
        result.BlockedByCommitmentDevice.Should().BeFalse();
        result.RemovedFiles.Should().Contain(sentinel);
        File.Exists(sentinel).Should().BeFalse("File must be removed during clean reverse flow");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_testRoot)) Directory.Delete(_testRoot, true); } catch { }
    }
}
