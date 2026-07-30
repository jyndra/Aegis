namespace Aegis.Core.Models;

/// <summary>
/// Result of rule engine evaluation.
/// </summary>
public record EvaluationResult(
    FilterDecision Decision,
    string Reason,
    FilterSeverity Severity,
    string Action,
    string ComponentState,
    int? RetryAfterSeconds
);
