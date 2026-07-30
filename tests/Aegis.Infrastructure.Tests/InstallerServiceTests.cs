using System.IO;
using Aegis.Core.Interfaces;
using Aegis.Core.Models;
using Aegis.Infrastructure.Deployment;
using FluentAssertions;
using Moq;
using Xunit;

namespace Aegis.Infrastructure.Tests;

public class InstallerServiceTests : IDisposable
{
    private readonly Mock<IStorageService> _mockStorage = new();
    private readonly InstallerService _installer;
    private readonly string _testRoot;

    public InstallerServiceTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "Aegis_InstallTest_" + Guid.NewGuid().ToString("N")[..8]);
        _installer = new InstallerService(_mockStorage.Object);
    }

    [Fact]
    public async Task InstallAsync_CreatesSubdirectoriesAndDeploysDefaultPolicies()
    {
        var options = new InstallOptions(OverrideInstallPath: _testRoot, RegisterWindowsService: false, DeployDefaultPolicies: true);
        
        _mockStorage.Setup(s => s.InitializeDatabaseAsync(It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

        var result = await _installer.InstallAsync(options);

        result.Success.Should().BeTrue();
        result.InstallRootPath.Should().Be(_testRoot);
        result.DeployedFiles.Should().HaveCount(2);

        // Verify subdirectories exist
        Directory.Exists(Path.Combine(_testRoot, "policies")).Should().BeTrue();
        Directory.Exists(Path.Combine(_testRoot, "models")).Should().BeTrue();
        Directory.Exists(Path.Combine(_testRoot, "logs")).Should().BeTrue();
        Directory.Exists(Path.Combine(_testRoot, "backups")).Should().BeTrue();
        Directory.Exists(Path.Combine(_testRoot, "data")).Should().BeTrue();

        // Verify deployed policy files
        File.Exists(Path.Combine(_testRoot, "policies", "keywords-default.json")).Should().BeTrue();
        File.Exists(Path.Combine(_testRoot, "policies", "regex-default.json")).Should().BeTrue();

        // Verify database schema initialization was invoked
        _mockStorage.Verify(s => s.InitializeDatabaseAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RestorePoliciesAsync_OverwritesAndRestoresDefaultPolicyFiles()
    {
        Directory.CreateDirectory(Path.Combine(_testRoot, "policies"));
        string keywordsPath = Path.Combine(_testRoot, "policies", "keywords-default.json");
        await File.WriteAllTextAsync(keywordsPath, "{ \"corrupted\": true }");

        bool success = await _installer.RestorePoliciesAsync(_testRoot);

        success.Should().BeTrue();
        string restoredContent = await File.ReadAllTextAsync(keywordsPath);
        restoredContent.Should().Contain("porn");
        restoredContent.Should().NotContain("corrupted");
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
