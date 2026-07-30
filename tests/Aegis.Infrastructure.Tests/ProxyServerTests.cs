using System.Net.Sockets;
using System.Text;
using Aegis.Core.Configuration;
using Aegis.Core.Models;
using Aegis.Infrastructure.Proxy;
using Aegis.Infrastructure.Storage;
using Aegis.Infrastructure.Time;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Aegis.Infrastructure.Tests;

public class ProxyServerTests : IDisposable
{
    private readonly Mock<IRuleEngine> _mockRuleEngine;
    private readonly Mock<IEventRepository> _mockEventRepo;
    private readonly ProxyServer _proxyServer;

    public ProxyServerTests()
    {
        _mockRuleEngine = new Mock<IRuleEngine>();
        _mockEventRepo = new Mock<IEventRepository>();
        var timeProvider = new SystemTimeProvider();

        var proxyOpts = Options.Create(new ProxyOptions { Enabled = true, ListenPort = 19081, ListenAddress = "127.0.0.1" });
        _proxyServer = new ProxyServer(_mockRuleEngine.Object, _mockEventRepo.Object, timeProvider, proxyOpts, NullLogger<ProxyServer>.Instance);
    }

    [Fact]
    public async Task StartAndStopAsync_ControlsProxyLifecycle()
    {
        await _proxyServer.StartAsync();
        _proxyServer.IsRunning.Should().BeTrue();

        await _proxyServer.StopAsync();
        _proxyServer.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task HandleClientAsync_Returns403Forbidden_WhenRuleEngineBlocks()
    {
        _mockRuleEngine.Setup(r => r.EvaluateAsync(It.IsAny<EvaluationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationResult(
                Decision: FilterDecision.Block,
                Reason: "Proxy Block Test",
                Severity: FilterSeverity.Critical,
                Action: "Block",
                ComponentState: "Protected",
                RetryAfterSeconds: null
            ));

        await _proxyServer.StartAsync();

        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", 19081);

        using var stream = client.GetStream();
        byte[] reqBytes = Encoding.ASCII.GetBytes("GET http://badsite.com/test HTTP/1.1\r\nHost: badsite.com\r\n\r\n");
        await stream.WriteAsync(reqBytes);

        byte[] resBuffer = new byte[1024];
        int read = await stream.ReadAsync(resBuffer);
        string responseText = Encoding.ASCII.GetString(resBuffer, 0, read);

        responseText.Should().Contain("HTTP/1.1 403 Forbidden");
        responseText.Should().Contain("Proxy Block Test");

        await _proxyServer.StopAsync();
    }

    public void Dispose()
    {
        _proxyServer.StopAsync().GetAwaiter().GetResult();
    }
}
