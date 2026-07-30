using Aegis.Core.Configuration;
using Aegis.Infrastructure.Rules;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Aegis.Infrastructure.Tests;

public class KeywordEngineTests
{
    private readonly KeywordEngine _keywordEngine;

    public KeywordEngineTests()
    {
        var opts = Options.Create(new FilteringOptions());
        _keywordEngine = new KeywordEngine(opts, NullLogger<KeywordEngine>.Instance);
    }

    [Fact]
    public async Task MatchScoreAsync_ReturnsScore_WhenKeywordPresent()
    {
        int score = await _keywordEngine.MatchScoreAsync("watch porn videos online");

        score.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task MatchScoreAsync_ExtractsUrlQueryParameter_AndEvaluates()
    {
        int score = await _keywordEngine.MatchScoreAsync("https://www.google.com/search?q=free+porn+clips");

        score.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task MatchScoreAsync_ReturnsZero_ForCleanContent()
    {
        int score = await _keywordEngine.MatchScoreAsync("https://google.com/search?q=dotnet+8+tutorials");

        score.Should().Be(0);
    }
}
