using Aegis.Core.Configuration;
using Aegis.Core.Interfaces;
using Aegis.Core.Models;
using Aegis.Service;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Aegis.Service.Tests;

public class AegisBackgroundServiceTests
{
    [Fact]
    public async Task ExecuteAsync_InitializesDatabase_AndRecordsInitialHealth()
    {
        var mockStorage = new Mock<IStorageService>();
        mockStorage.Setup(s => s.CheckIntegrityAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var mockDns = new Mock<IDnsFilter>();
        var mockProxy = new Mock<IProxyServer>();
        var mockHealth = new Mock<IHealthReporter>();
        var mockIntegrity = new Mock<IIntegrityEngine>();
        mockIntegrity.Setup(i => i.RunBootAuditAsync(It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new IntegrityReport(true, new List<IntegrityCheckResult>(), DateTimeOffset.UtcNow));

        var mockTime = new Mock<ITimeProvider>();
        mockTime.Setup(t => t.UtcNow).Returns(DateTimeOffset.UtcNow);

        var options = Options.Create(new ServiceOptions { HealthCheckIntervalSeconds = 1, IntegrityCheckIntervalSeconds = 1 });
        var service = new AegisBackgroundService(
            mockStorage.Object,
            mockDns.Object,
            mockProxy.Object,
            mockHealth.Object,
            mockIntegrity.Object,
            mockTime.Object,
            options,
            NullLogger<AegisBackgroundService>.Instance
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await service.StartAsync(cts.Token);
        await service.StopAsync(CancellationToken.None);

        mockStorage.Verify(s => s.InitializeDatabaseAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockHealth.Verify(h => h.RecordHealthAsync("Service", "Healthy", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }
}
