using System.Data.Common;
using System.Text.RegularExpressions;
using Aegis.Core.Configuration;
using Aegis.Core.Interfaces;
using Aegis.Core.Models;
using Aegis.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.Rules;

public class CustomPolicyService : ICustomPolicyService
{
    private readonly IBlocklistRepository _blocklistRepo;
    private readonly IDnsFilter _dnsFilter;
    private readonly IRegexEngine _regexEngine;
    private readonly IKeywordEngine _keywordEngine;
    private readonly ICommitLockEngine _commitLockEngine;
    private readonly SqliteStorageService _storageService;
    private readonly IOptions<LockOptions> _lockOptions;
    private readonly ILogger<CustomPolicyService> _logger;

    public CustomPolicyService(
        IBlocklistRepository blocklistRepo,
        IDnsFilter dnsFilter,
        IRegexEngine regexEngine,
        IKeywordEngine keywordEngine,
        ICommitLockEngine commitLockEngine,
        SqliteStorageService storageService,
        IOptions<LockOptions> lockOptions,
        ILogger<CustomPolicyService> logger)
    {
        _blocklistRepo = blocklistRepo;
        _dnsFilter = dnsFilter;
        _regexEngine = regexEngine;
        _keywordEngine = keywordEngine;
        _commitLockEngine = commitLockEngine;
        _storageService = storageService;
        _lockOptions = lockOptions;
        _logger = logger;
    }

    public async Task<bool> AddCustomWebsiteAsync(string domain, CancellationToken cancellationToken = default)
    {
        string norm = BlocklistRepository.NormalizeDomain(domain);
        if (string.IsNullOrEmpty(norm)) return false;

        _logger.LogInformation("Adding custom website block rule for domain: {Domain}", norm);
        await _blocklistRepo.AddDomainAsync(norm, "UserCustom", cancellationToken);

        // Instant hot reload in DNS filter
        await _dnsFilter.ReloadBlocklistAsync(cancellationToken);
        return true;
    }

    public async Task<bool> AddCustomKeywordAsync(string keyword, int weight = 50, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return false;

        string cleanKeyword = keyword.Trim().ToLowerInvariant();
        _logger.LogInformation("Adding custom keyword rule: '{Keyword}' (Weight: {Weight})", cleanKeyword, weight);

        await InsertBlockedRuleAsync("Keyword", cleanKeyword, weight, "UserCustom", cancellationToken);

        // Instant hot reload in Keyword engine
        await _keywordEngine.ReloadKeywordsAsync(cancellationToken);
        return true;
    }

    public async Task<bool> AddCustomRegexAsync(string pattern, int score = 50, string description = "", CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return false;

        // Validate regex syntax prior to persisting
        try
        {
            _ = new Regex(pattern, RegexOptions.IgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid custom regex pattern submission: '{Pattern}'", pattern);
            throw new ArgumentException($"Invalid regular expression pattern: {ex.Message}", nameof(pattern), ex);
        }

        _logger.LogInformation("Adding custom regex rule: '{Pattern}' (Score: {Score})", pattern, score);
        await InsertBlockedRuleAsync("Regex", pattern, score, string.IsNullOrWhiteSpace(description) ? "UserCustomRegex" : description, cancellationToken);

        // Instant hot reload in Regex engine
        await _regexEngine.ReloadPatternsAsync(cancellationToken);
        return true;
    }

    public async Task<(bool Success, string Message)> RemoveCustomRuleAsync(long ruleId, CancellationToken cancellationToken = default)
    {
        // Enforce the One-Way Protection Ratchet
        var lockStatus = await _commitLockEngine.GetStatusAsync(cancellationToken);
        bool ratchetLocked = lockStatus.Locked && !_lockOptions.Value.BypassLockForTesting;

        if (ratchetLocked)
        {
            string blockMsg = "Protection Ratchet Active: Rule modification or deletion is strictly locked during the active 25-day commitment period. You can only ADD rules while locked.";
            _logger.LogWarning("CUSTOM RULE REMOVAL REJECTED: {Reason}", blockMsg);
            return (false, blockMsg);
        }

        using var connection = _storageService.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM blocked_rules WHERE id = $id;";
        AddParameter(cmd, "$id", ruleId);

        int rows = await cmd.ExecuteNonQueryAsync(cancellationToken);
        if (rows > 0)
        {
            _logger.LogInformation("Successfully removed custom rule ID: {Id}", ruleId);
            await _keywordEngine.ReloadKeywordsAsync(cancellationToken);
            await _regexEngine.ReloadPatternsAsync(cancellationToken);
            return (true, $"Custom rule ID {ruleId} removed.");
        }

        return (false, $"Custom rule ID {ruleId} not found.");
    }

    public async Task<CustomRulesOverview> GetCustomRulesOverviewAsync(CancellationToken cancellationToken = default)
    {
        var domainHashes = await _blocklistRepo.GetAllDomainHashesAsync(cancellationToken);
        var rules = await _blocklistRepo.GetRulesAsync(cancellationToken);
        var lockStatus = await _commitLockEngine.GetStatusAsync(cancellationToken);

        return new CustomRulesOverview(
            Websites: domainHashes.ToList(),
            Rules: rules,
            CommitmentLockActive: lockStatus.Locked,
            TestModeActive: _lockOptions.Value.BypassLockForTesting
        );
    }

    private async Task InsertBlockedRuleAsync(string ruleType, string pattern, int weight, string source, CancellationToken cancellationToken)
    {
        using var connection = _storageService.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO blocked_rules (rule_type, pattern, enabled, source, weight, created_at)
            VALUES ($type, $pattern, 1, $source, $weight, $created_at);
        ";
        AddParameter(cmd, "$type", ruleType);
        AddParameter(cmd, "$pattern", pattern);
        AddParameter(cmd, "$source", source);
        AddParameter(cmd, "$weight", weight);
        AddParameter(cmd, "$created_at", DateTimeOffset.UtcNow.ToString("o"));

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var param = command.CreateParameter();
        param.ParameterName = name;
        param.Value = value;
        command.Parameters.Add(param);
    }
}
