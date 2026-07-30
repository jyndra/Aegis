using Aegis.Infrastructure.Time;
using FluentAssertions;
using Xunit;

namespace Aegis.Infrastructure.Tests;

public class SystemTimeProviderTests
{
    [Fact]
    public void SystemTimeProvider_ReturnsCurrentUtcTimeAndMonotonicTicks()
    {
        var provider = new SystemTimeProvider();

        var before = DateTimeOffset.UtcNow;
        var utc = provider.UtcNow;
        var after = DateTimeOffset.UtcNow;

        utc.Should().BeOnOrAfter(before.AddSeconds(-1)).And.BeOnOrBefore(after.AddSeconds(1));
        provider.MonotonicTicks.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetElapsedTimeTicks_CalculatesElapsedTime()
    {
        var provider = new SystemTimeProvider();

        long start = provider.MonotonicTicks;
        Thread.Sleep(10);
        long elapsed = provider.GetElapsedTimeTicks(start);

        elapsed.Should().BeGreaterThan(0);
    }
}
