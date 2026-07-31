using Aegis.Core.Models;

namespace Aegis.Core.Interfaces;

public record CustomRulesOverview(
    IReadOnlyList<string> Websites,
    IReadOnlyList<BlockedRule> Rules,
    bool CommitmentLockActive,
    bool TestModeActive
);

public interface ICustomPolicyService
{
    Task<bool> AddCustomWebsiteAsync(string domain, CancellationToken cancellationToken = default);
    Task<bool> AddCustomKeywordAsync(string keyword, int weight = 50, CancellationToken cancellationToken = default);
    Task<bool> AddCustomRegexAsync(string pattern, int score = 50, string description = "", CancellationToken cancellationToken = default);
    Task<(bool Success, string Message)> RemoveCustomRuleAsync(long ruleId, CancellationToken cancellationToken = default);
    Task<CustomRulesOverview> GetCustomRulesOverviewAsync(CancellationToken cancellationToken = default);
}
