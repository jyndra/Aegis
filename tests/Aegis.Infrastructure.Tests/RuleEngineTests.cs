using Aegis.Core.Configuration;
using Aegis.Core.Interfaces;
using Aegis.Core.Models;
using Aegis.Infrastructure.Rules;
using Aegis.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Aegis.Infrastructure.Tests;

public class RuleEngineTests
{
    private readonly Mock<IBlocklistRepository> _mockBlocklistRepo;
    private readonly RegexEngine _regexEngine;
    private readonly KeywordEngine _keywordEngine;
    private readonly RuleEngine _ruleEngine;

    public RuleEngineTests()
    {
        _mockBlocklistRepo = new Mock<IBlocklistRepository>();

        var filterOpts = Options.Create(new FilteringOptions { ScoreThreshold = 70, RuleEvaluationTimeoutMs = 100 });
        _regexEngine = new RegexEngine(filterOpts, NullLogger<RegexEngine>.Instance);
        _keywordEngine = new KeywordEngine(filterOpts, NullLogger<KeywordEngine>.Instance);

        _ruleEngine = new RuleEngine(_mockBlocklistRepo.Object, _regexEngine, _keywordEngine, filterOpts, NullLogger<RuleEngine>.Instance);
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsInstantBlock_WhenDomainInBlocklist()
    {
        _mockBlocklistRepo.Setup(r => r.ContainsDomainHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var req = new EvaluationRequest("https://badsite.com/home", "badsite.com", "/", null, "Bad Site", null, "Chrome", "Extension", DateTimeOffset.UtcNow);

        var result = await _ruleEngine.EvaluateAsync(req);

        result.Decision.Should().Be(FilterDecision.Block);
        result.Reason.Should().Contain("blocklist");
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsBlock_WhenScoreExceedsThreshold()
    {
        _mockBlocklistRepo.Setup(r => r.ContainsDomainHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var req = new EvaluationRequest("https://unknownsite.org/watch-porn-videos", "unknownsite.org", "/watch-porn-videos", null, "Watch Porn Clips", null, "Chrome", "Extension", DateTimeOffset.UtcNow);

        var result = await _ruleEngine.EvaluateAsync(req);

        result.Decision.Should().Be(FilterDecision.Block);
        result.Reason.Should().Contain("threshold exceeded");
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsAllow_ForCleanContent()
    {
        _mockBlocklistRepo.Setup(r => r.ContainsDomainHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var req = new EvaluationRequest("https://dotnet.microsoft.com", "dotnet.microsoft.com", "/", null, ".NET Documentation", null, "Chrome", "Extension", DateTimeOffset.UtcNow);

        var result = await _ruleEngine.EvaluateAsync(req);

        result.Decision.Should().Be(FilterDecision.Allow);
        result.Reason.Should().Contain("safe limits");
    }
}
