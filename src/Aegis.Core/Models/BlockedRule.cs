namespace Aegis.Core.Models;

/// <summary>
/// Rule definition for blocking domains, patterns, or keywords.
/// </summary>
public record BlockedRule(
    long Id,
    string RuleType,
    string Pattern,
    bool Enabled,
    string Source,
    int Weight,
    DateTimeOffset CreatedAt
);
