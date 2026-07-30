using Aegis.Core.Configuration;
using Aegis.Core.Interfaces;
using Aegis.Core.Models;
using Aegis.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.Rules;

public class RuleEngine : IRuleEngine
{
    private readonly IBlocklistRepository _blocklistRepo;
    private readonly IRegexEngine _regexEngine;
    private readonly IKeywordEngine _keywordEngine;
    private readonly IAiTextClassifier? _aiTextClassifier;
    private readonly IOptions<FilteringOptions> _options;
    private readonly ILogger<RuleEngine> _logger;

    public RuleEngine(
        IBlocklistRepository blocklistRepo,
        IRegexEngine regexEngine,
        IKeywordEngine keywordEngine,
        IOptions<FilteringOptions> options,
        ILogger<RuleEngine> logger,
        IAiTextClassifier? aiTextClassifier = null)
    {
        _blocklistRepo = blocklistRepo;
        _regexEngine = regexEngine;
        _keywordEngine = keywordEngine;
        _options = options;
        _logger = logger;
        _aiTextClassifier = aiTextClassifier;
    }

    public async Task<EvaluationResult> EvaluateAsync(EvaluationRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Url))
        {
            return new EvaluationResult(
                Decision: FilterDecision.Allow,
                Reason: "Empty or invalid evaluation request",
                Severity: FilterSeverity.Info,
                Action: "Allow",
                ComponentState: "Protected",
                RetryAfterSeconds: null
            );
        }

        int timeoutMs = Math.Max(1, _options.Value.RuleEvaluationTimeoutMs);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));

        try
        {
            // Stage 1: Domain Blocklist Check (Instant priority)
            string domain = request.Domain ?? ExtractDomainFromUrl(request.Url);
            string normDomain = BlocklistRepository.NormalizeDomain(domain);

            if (!string.IsNullOrEmpty(normDomain))
            {
                string domainHash = BlocklistRepository.ComputeDomainHash(normDomain);
                bool inBlocklist = await _blocklistRepo.ContainsDomainHashAsync(domainHash, cts.Token);

                if (inBlocklist)
                {
                    _logger.LogInformation("RuleEngine INSTANT BLOCK: Domain '{Domain}' in blocklist.", normDomain);
                    return new EvaluationResult(
                        Decision: FilterDecision.Block,
                        Reason: $"Domain '{normDomain}' matches Aegis blocklist",
                        Severity: FilterSeverity.Critical,
                        Action: "Block",
                        ComponentState: "Protected",
                        RetryAfterSeconds: null
                    );
                }
            }

            // Stage 2 & 3: Parallel Regex & Keyword Evaluation
            string targetText = $"{request.Url} {request.Title ?? ""} {request.Query ?? ""}";

            var regexTask = _regexEngine.EvaluateRegexScoreAsync(targetText, cts.Token);
            var keywordTask = _keywordEngine.MatchScoreAsync(targetText, cts.Token);

            await Task.WhenAll(regexTask, keywordTask);

            int regexScore = await regexTask;
            int keywordScore = await keywordTask;
            int totalScore = regexScore + keywordScore;

            int threshold = _options.Value.ScoreThreshold;

            // Stage 4: AI Text Classification for Ambiguous Scores
            if (totalScore < threshold && totalScore >= threshold - 35 && _aiTextClassifier != null)
            {
                var aiResult = await _aiTextClassifier.ClassifyTextAsync(targetText, cts.Token);
                if (aiResult.IsExplicit || aiResult.ConfidenceScore >= 0.70)
                {
                    int boost = (int)(aiResult.ConfidenceScore * 40);
                    totalScore += boost;
                    _logger.LogInformation("RuleEngine AI BOOST: Target '{Url}' boosted by +{Boost} (Confidence: {Confidence}) to new total {Total}.",
                        request.Url, boost, aiResult.ConfidenceScore, totalScore);
                }
            }

            _logger.LogDebug("RuleEngine Evaluated '{Url}': RegexScore={Regex}, KeywordScore={Keyword}, TotalScore={Total} (Threshold: {Threshold})",
                request.Url, regexScore, keywordScore, totalScore, threshold);

            if (totalScore >= threshold)
            {
                _logger.LogInformation("RuleEngine BLOCK: Target '{Url}' exceeded score threshold ({Total} >= {Threshold}).",
                    request.Url, totalScore, threshold);

                return new EvaluationResult(
                    Decision: FilterDecision.Block,
                    Reason: $"Content score threshold exceeded ({totalScore}/{threshold})",
                    Severity: FilterSeverity.Critical,
                    Action: "Block",
                    ComponentState: "Protected",
                    RetryAfterSeconds: null
                );
            }

            return new EvaluationResult(
                Decision: FilterDecision.Allow,
                Reason: $"Content score within safe limits ({totalScore}/{threshold})",
                Severity: FilterSeverity.Info,
                Action: "Allow",
                ComponentState: "Protected",
                RetryAfterSeconds: null
            );
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // Timeout: content was not evaluated — cannot claim it is harmful, so Allow but warn
            _logger.LogWarning("RuleEngine evaluation exceeded time budget ({Timeout}ms). Defaulting to Allow (content unevaluated).", timeoutMs);

            return new EvaluationResult(
                Decision: FilterDecision.Allow,
                Reason: $"Rule evaluation timed out ({timeoutMs}ms limit)",
                Severity: FilterSeverity.Warning,
                Action: "Allow",
                ComponentState: "Protected",
                RetryAfterSeconds: null
            );
        }
        catch (Exception ex)
        {
            // Hardening M10: Fail-closed per RECOVERY.md \u00a7 Principle 1: "Fail closed. When in doubt, block."
            // An exception here means the rule engine itself is broken — safer to block than to silently allow.
            _logger.LogError(ex, "RuleEngine unhandled error evaluating URL '{Url}'. Failing closed (Block).", request.Url);

            return new EvaluationResult(
                Decision: FilterDecision.Block,
                Reason: $"Rule evaluation failed: {ex.Message} (fail-closed)",
                Severity: FilterSeverity.Warning,
                Action: "Block",
                ComponentState: "Protected",
                RetryAfterSeconds: null
            );
        }
    }

    private static string ExtractDomainFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            return uri.Host;
        }
        catch
        {
            return string.Empty;
        }
    }
}
