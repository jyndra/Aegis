using Aegis.Core.Configuration;
using Aegis.Infrastructure.Rules;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Aegis.Infrastructure.Tests;

public class RegexEngineTests
{
    private readonly RegexEngine _regexEngine;

    public RegexEngineTests()
    {
        var opts = Options.Create(new FilteringOptions());
        _regexEngine = new RegexEngine(opts, NullLogger<RegexEngine>.Instance);
    }

    [Fact]
    public async Task EvaluateRegexScoreAsync_ReturnsScore_WhenPatternMatches()
    {
        int score = await _regexEngine.EvaluateRegexScoreAsync("https://example.com/watch-porn-online");

        score.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task EvaluateRegexScoreAsync_ReturnsZero_WhenNoPatternMatches()
    {
        int score = await _regexEngine.EvaluateRegexScoreAsync("https://wikipedia.org/wiki/Computer_science");

        score.Should().Be(0);
    }
}
