using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Aegis.Core.Configuration;
using Aegis.Core.Interfaces;
using Aegis.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.Rules;

public class KeywordEngine : IKeywordEngine
{
    private readonly IOptions<FilteringOptions> _options;
    private readonly ILogger<KeywordEngine> _logger;
    private volatile IReadOnlyList<KeywordRule> _rules = Array.Empty<KeywordRule>();

    public KeywordEngine(IOptions<FilteringOptions> options, ILogger<KeywordEngine> logger)
    {
        _options = options;
        _logger = logger;
        InitializeDefaultKeywords();
        _ = ReloadKeywordsAsync();
    }

    private void InitializeDefaultKeywords()
    {
        var defaultRules = new List<KeywordRule>
        {
            new("porn", 85, KeywordMatchType.WordBoundary),
            new("xxx", 85, KeywordMatchType.WordBoundary),
            new("hentai", 80, KeywordMatchType.WordBoundary),
            new("erotic", 50, KeywordMatchType.WordBoundary),
            new("nsfw", 50, KeywordMatchType.WordBoundary),
            new("sex video", 90, KeywordMatchType.Contains),
            new("watch porn", 100, KeywordMatchType.Contains)
        };

        _rules = defaultRules;
    }

    public Task<int> MatchScoreAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.FromResult(0);
        }

        string normalized = ExtractAndNormalizeText(text);
        int score = 0;
        var rules = _rules; // Atomic snapshot read

        foreach (var rule in rules)
        {
            if (cancellationToken.IsCancellationRequested) break;

            if (IsMatch(normalized, rule))
            {
                score += rule.Weight;
                _logger.LogDebug("Keyword matched: '{Keyword}' (MatchType: {MatchType}, Weight: {Weight})",
                    rule.Keyword, rule.MatchType, rule.Weight);
            }
        }

        return Task.FromResult(score);
    }

    public async Task ReloadKeywordsAsync(CancellationToken cancellationToken = default)
    {
        var paths = _options.Value.KeywordPackPaths;
        if (paths == null || paths.Count == 0) return;

        foreach (var relativePath in paths)
        {
            string fullPath = Path.IsPathRooted(relativePath)
                ? relativePath
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Aegis", relativePath);

            if (!File.Exists(fullPath))
            {
                _logger.LogDebug("Keyword pack file not found at {Path}, using compiled defaults.", fullPath);
                continue;
            }

            try
            {
                string json = await File.ReadAllTextAsync(fullPath, cancellationToken);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
                var pack = JsonSerializer.Deserialize<KeywordPack>(json, options);

                if (pack?.Rules != null && pack.Rules.Count > 0)
                {
                    var merged = new List<KeywordRule>(_rules);
                    foreach (var loadedRule in pack.Rules)
                    {
                        var existing = merged.FirstOrDefault(r => string.Equals(r.Keyword, loadedRule.Keyword, StringComparison.OrdinalIgnoreCase));
                        if (existing != null)
                        {
                            if (loadedRule.Weight > existing.Weight)
                            {
                                merged.Remove(existing);
                                merged.Add(loadedRule);
                            }
                        }
                        else
                        {
                            merged.Add(loadedRule);
                        }
                    }
                    _rules = merged; // Thread-safe atomic pointer swap
                    _logger.LogInformation("Loaded and merged {Count} keyword rules from pack '{Name}' v{Version} at {Path}",
                        _rules.Count, pack.Name, pack.Version, fullPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load or parse keyword pack file at {Path}", fullPath);
            }
        }
    }

    private static string ExtractAndNormalizeText(string input)
    {
        string text = input.Trim().ToLowerInvariant();

        // If URL, extract query parameters
        if (text.StartsWith("http://") || text.StartsWith("https://"))
        {
            try
            {
                var uri = new Uri(text);
                var queryParams = HttpUtility.ParseQueryString(uri.Query);
                var queryTerms = new List<string>();

                foreach (string key in new[] { "q", "query", "search", "p", "k", "keywords" })
                {
                    string? val = queryParams[key];
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        queryTerms.Add(val);
                    }
                }

                if (queryTerms.Count > 0)
                {
                    return string.Join(" ", queryTerms).ToLowerInvariant();
                }

                return uri.PathAndQuery.ToLowerInvariant();
            }
            catch
            {
                // Fallback to raw text
            }
        }

        return text;
    }

    private static bool IsMatch(string text, KeywordRule rule)
    {
        string kw = rule.Keyword.ToLowerInvariant();

        return rule.MatchType switch
        {
            KeywordMatchType.Exact => string.Equals(text, kw, StringComparison.OrdinalIgnoreCase),
            KeywordMatchType.Contains => text.Contains(kw, StringComparison.OrdinalIgnoreCase),
            KeywordMatchType.WordBoundary => Regex.IsMatch(text, $@"\b{Regex.Escape(kw)}\b", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(5)),
            _ => text.Contains(kw, StringComparison.OrdinalIgnoreCase)
        };
    }
}
