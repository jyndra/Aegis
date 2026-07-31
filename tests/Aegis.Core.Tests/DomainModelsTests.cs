using Aegis.Core.Models;
using FluentAssertions;
using Xunit;

namespace Aegis.Core.Tests;

public class DomainModelsTests
{
    [Fact]
    public void LockState_RecordPropertiesAndEqualityWork()
    {
        var now = DateTimeOffset.UtcNow;
        var state1 = new LockState(
            Locked: true,
            LockStartedAt: now,
            LockExpiresAt: now.AddDays(25),
            UnlockRequestedAt: null,
            UnlockStage: 0,
            FailedAttempts: 0,
            UpdatedAt: now
        );

        var state2 = state1 with { FailedAttempts = 0 };

        state1.Should().Be(state2);
        state1.Locked.Should().BeTrue();
    }

    [Fact]
    public void EvaluationRequestAndResult_RecordPropertiesWork()
    {
        var req = new EvaluationRequest(
            Url: "https://example.com/test",
            Domain: "example.com",
            Path: "/test",
            Query: null,
            Title: "Test Page",
            Referrer: null,
            Browser: "Chrome",
            Component: "Extension",
            Timestamp: DateTimeOffset.UtcNow
        );

        var res = new EvaluationResult(
            Decision: FilterDecision.Allow,
            Reason: "Clean domain",
            Severity: FilterSeverity.Info,
            Action: "Allow",
            ComponentState: "Protected",
            RetryAfterSeconds: null
        );

        req.Url.Should().Be("https://example.com/test");
        res.Decision.Should().Be(FilterDecision.Allow);
    }
}
