using System.Text.RegularExpressions;
using Aegis.Core.Interfaces;
using Aegis.Core.Models;

namespace Aegis.Infrastructure.Ai;

public partial class AiTextClassifier : IAiTextClassifier
{
    private static readonly Dictionary<string, double> UnigramWeights = new(StringComparer.OrdinalIgnoreCase)
    {
        { "xxx", 0.95 },
        { "porn", 0.95 },
        { "porno", 0.95 },
        { "hentai", 0.90 },
        { "nude", 0.70 },
        { "nudes", 0.75 },
        { "erotic", 0.75 },
        { "sex", 0.60 },
        { "adult", 0.40 },
        { "camgirl", 0.85 },
        { "onlyfans", 0.65 },
        { "nsfw", 0.80 },
        { "fetish", 0.70 },
        { "uncensored", 0.60 }
    };

    private static readonly Dictionary<string, double> BigramWeights = new(StringComparer.OrdinalIgnoreCase)
    {
        { "adult video", 0.85 },
        { "adult cam", 0.90 },
        { "sex cam", 0.95 },
        { "free porn", 0.98 },
        { "nude pics", 0.90 },
        { "hardcore sex", 0.98 },
        { "live chat", 0.40 },
        { "dating app", 0.35 }
    };

    [GeneratedRegex(@"\b\w+\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex WordRegex();

    public Task<TextClassificationResult> ClassifyTextAsync(string? content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Task.FromResult(new TextClassificationResult(
                IsExplicit: false,
                ConfidenceScore: 0.0,
                Category: "Safe",
                Summary: "Empty content."
            ));
        }

        // Staff Engineer Optimization: Cap analysis to first 10,000 chars to avoid excessive GC allocation and guarantee <5ms execution budget
        string analysisText = content.Length > 10000 ? content[..10000] : content;

        var matches = WordRegex().Matches(analysisText);
        var tokens = new List<string>(matches.Count);
        foreach (Match match in matches)
        {
            tokens.Add(match.Value);
        }

        if (tokens.Count == 0)
        {
            return Task.FromResult(new TextClassificationResult(
                IsExplicit: false,
                ConfidenceScore: 0.0,
                Category: "Safe",
                Summary: "No actionable tokens extracted."
            ));
        }

        double totalWeight = 0.0;
        int hitCount = 0;
        List<string> hitWords = new();

        // Check Unigrams
        foreach (var token in tokens)
        {
            if (UnigramWeights.TryGetValue(token, out double weight))
            {
                totalWeight += weight;
                hitCount++;
                if (hitWords.Count < 5) hitWords.Add(token);
            }
        }

        // Check Bigrams
        for (int i = 0; i < tokens.Count - 1; i++)
        {
            string bigram = $"{tokens[i]} {tokens[i + 1]}";
            if (BigramWeights.TryGetValue(bigram, out double weight))
            {
                totalWeight += weight * 1.5; // Premium for phrase match
                hitCount += 2;
                if (hitWords.Count < 5) hitWords.Add(bigram);
            }
        }

        // Compute normalized probability score using Sigmoid / decay formula over hit ratio and total weight
        double hitRatio = (double)hitCount / Math.Max(1, Math.Min(tokens.Count, 100)); // Cap denominator to evaluate density in short snippets
        double confidenceScore = 1.0 - Math.Exp(-0.7 * (totalWeight + (hitRatio * 5.0)));
        confidenceScore = Math.Round(Math.Min(1.0, Math.Max(0.0, confidenceScore)), 3);

        string category;
        bool isExplicit = false;

        if (confidenceScore >= 0.75)
        {
            category = "ExplicitAdult";
            isExplicit = true;
        }
        else if (confidenceScore >= 0.40)
        {
            category = "Suggestive";
        }
        else
        {
            category = "Safe";
        }

        string summary = hitWords.Count > 0 
            ? $"Matched {hitCount} high-probability tokens/phrases: {string.Join(", ", hitWords)}."
            : "No explicit adult language markers identified.";

        return Task.FromResult(new TextClassificationResult(
            IsExplicit: isExplicit,
            ConfidenceScore: confidenceScore,
            Category: category,
            Summary: summary
        ));
    }
}
