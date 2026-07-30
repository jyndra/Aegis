using System.Text.Json;
using System.Text.RegularExpressions;
using Aegis.Core.Configuration;
using Aegis.Core.Errors;
using Aegis.Core.Interfaces;
using Aegis.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.Rules;

public class CompiledRegexRule
{
    public Regex Regex { get; }
    public int Weight { get; }
    public string Category { get; }
    public string Description { get; }

    public CompiledRegexRule(string pattern, int weight, string category, string description, TimeSpan timeout)
    {
        Regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase, timeout);
        Weight = weight;
        Category = category;
        Description = description;
    }
}

public class RegexEngine : IRegexEngine
{
    private readonly IOptions<FilteringOptions> _options;
    private readonly ILogger<RegexEngine> _logger;
    private volatile IReadOnlyList<CompiledRegexRule> _compiledRules = Array.Empty<CompiledRegexRule>();

    private static readonly TimeSpan DefaultMatchTimeout = TimeSpan.FromMilliseconds(5);

    public RegexEngine(IOptions<FilteringOptions> options, ILogger<RegexEngine> logger)
    {
        _options = options;
        _logger = logger;
        InitializeDefaultRules();
        _ = ReloadPatternsAsync();
    }

    private void InitializeDefaultRules()
    {
        var defaultPacks = new List<RegexRule>
        {
            new(@"\b(porn|porno|xxx|xnxx|xvideos|redtube|youporn|chaturbate)\b", 80, "ExplicitDomain", "Known explicit domain heuristics"),
            new(@"\b(hentai|erotic|nsfw|sex|camgirl|stripclub|playboy)\b", 45, "AdultCategory", "Adult category keywords"),
            new(@"\b(free-sex-videos|watch-porn-online|hd-porn-clips)\b", 75, "HighRiskUrl", "High-risk URL patterns")
        };

        var list = new List<CompiledRegexRule>();
        foreach (var rule in defaultPacks)
        {
            try
            {
                list.Add(new CompiledRegexRule(rule.Pattern, rule.Weight, rule.Category, rule.Description, DefaultMatchTimeout));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to compile default regex pattern '{Pattern}'", rule.Pattern);
            }
        }

        _compiledRules = list;
    }

    public Task<int> EvaluateRegexScoreAsync(string target, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return Task.FromResult(0);
        }

        int score = 0;
        var rules = _compiledRules; // Atomic snapshot read

        foreach (var compiledRule in rules)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                if (compiledRule.Regex.IsMatch(target))
                {
                    score += compiledRule.Weight;
                    _logger.LogDebug("Regex rule matched: '{Pattern}' (Category: {Category}, Weight: {Weight})",
                        compiledRule.Regex.ToString(), compiledRule.Category, compiledRule.Weight);
                }
            }
            catch (RegexMatchTimeoutException)
            {
                _logger.LogWarning("ReDoS protection triggered! Regex match timed out for pattern '{Pattern}' ({ErrorCode}).",
                    compiledRule.Regex.ToString(), AegisErrorCodes.RuleEvaluationTimeout);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating regex rule for pattern '{Pattern}'", compiledRule.Regex.ToString());
            }
        }

        return Task.FromResult(score);
    }

    public async Task ReloadPatternsAsync(CancellationToken cancellationToken = default)
    {
        var paths = _options.Value.RegexPackPaths;
        if (paths == null || paths.Count == 0) return;

        foreach (var relativePath in paths)
        {
            string fullPath = Path.IsPathRooted(relativePath)
                ? relativePath
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Aegis", relativePath);

            if (!File.Exists(fullPath))
            {
                _logger.LogDebug("Regex pack file not found at {Path}, using compiled defaults.", fullPath);
                continue;
            }

            try
            {
                string json = await File.ReadAllTextAsync(fullPath, cancellationToken);
                var pack = JsonSerializer.Deserialize<RegexPack>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (pack?.Rules != null && pack.Rules.Count > 0)
                {
                    var newRules = new List<CompiledRegexRule>();
                    foreach (var rule in pack.Rules)
                    {
                        newRules.Add(new CompiledRegexRule(rule.Pattern, rule.Weight, rule.Category, rule.Description, DefaultMatchTimeout));
                    }

                    _compiledRules = newRules; // Thread-safe atomic pointer swap
                    _logger.LogInformation("Loaded {Count} regex rules from pack '{Name}' v{Version} at {Path}",
                        _compiledRules.Count, pack.Name, pack.Version, fullPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load or parse regex pack file at {Path}", fullPath);
            }
        }
    }
}
