using Aegis.Core.Configuration;
using FluentAssertions;
using Xunit;

namespace Aegis.Core.Tests;

public class ConfigurationOptionsTests
{
    [Fact]
    public void ServiceOptions_HasCorrectDefaultsAndSectionName()
    {
        ServiceOptions.SectionName.Should().Be("service");

        var opts = new ServiceOptions();
        opts.ApiPort.Should().Be(9443);
        opts.ApiBindAddress.Should().Be("127.0.0.1");
        opts.HealthCheckIntervalSeconds.Should().Be(60);
        opts.IntegrityCheckIntervalSeconds.Should().Be(300);
        opts.LogLevel.Should().Be("Information");
    }

    [Fact]
    public void DnsOptions_HasCorrectDefaultsAndSectionName()
    {
        DnsOptions.SectionName.Should().Be("dns");

        var opts = new DnsOptions();
        opts.Enabled.Should().BeTrue();
        opts.ListenPort.Should().Be(53);
        opts.UpstreamServers.Should().Contain("1.1.1.1");
    }

    [Fact]
    public void FilteringOptions_HasCorrectDefaultsAndSectionName()
    {
        FilteringOptions.SectionName.Should().Be("filtering");

        var opts = new FilteringOptions();
        opts.RuleEvaluationTimeoutMs.Should().Be(5);
        opts.ScoreThreshold.Should().Be(70);
    }

    [Fact]
    public void LockOptions_HasCorrectDefaultsAndSectionName()
    {
        LockOptions.SectionName.Should().Be("lock");

        var opts = new LockOptions();
        opts.DefaultLockDays.Should().Be(25);
        opts.UnlockCooldownMinutes.Should().Be(60);
        opts.UnlockStages.Should().Be(3);
    }

    [Fact]
    public void ProxyOptions_HasCorrectDefaultsAndSectionName()
    {
        ProxyOptions.SectionName.Should().Be("proxy");

        var opts = new ProxyOptions();
        opts.Enabled.Should().BeFalse();
        opts.ListenPort.Should().Be(8080);
    }
}
