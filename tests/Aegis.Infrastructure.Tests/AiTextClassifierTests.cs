using Aegis.Infrastructure.Ai;
using FluentAssertions;
using Xunit;

namespace Aegis.Infrastructure.Tests;

public class AiTextClassifierTests
{
    private readonly AiTextClassifier _classifier = new();

    [Fact]
    public async Task ClassifyTextAsync_WithEmptyContent_ReturnsSafe()
    {
        var result = await _classifier.ClassifyTextAsync("");

        result.IsExplicit.Should().BeFalse();
        result.ConfidenceScore.Should().Be(0.0);
        result.Category.Should().Be("Safe");
    }

    [Fact]
    public async Task ClassifyTextAsync_WithBenignText_ReturnsSafe()
    {
        string benignText = "This is a computer science engineering lecture about distributed database consensus algorithms and network protocols.";
        var result = await _classifier.ClassifyTextAsync(benignText);

        result.IsExplicit.Should().BeFalse();
        result.ConfidenceScore.Should().BeLessThan(0.40);
        result.Category.Should().Be("Safe");
    }

    [Fact]
    public async Task ClassifyTextAsync_WithExplicitUnigramsAndBigrams_ReturnsExplicitAdult()
    {
        string explicitText = "Watch free porn videos and adult cam live streaming xxx hardcore sex nudes online.";
        var result = await _classifier.ClassifyTextAsync(explicitText);

        result.IsExplicit.Should().BeTrue();
        result.ConfidenceScore.Should().BeGreaterThanOrEqualTo(0.75);
        result.Category.Should().Be("ExplicitAdult");
        result.Summary.Should().Contain("high-probability tokens");
    }

    [Fact]
    public async Task ClassifyTextAsync_WithMildKeywords_ReturnsSuggestiveOrSafe()
    {
        string mildText = "Join our dating app to find local matches.";
        var result = await _classifier.ClassifyTextAsync(mildText);

        result.IsExplicit.Should().BeFalse();
        result.ConfidenceScore.Should().BeLessThan(0.75);
    }
}
