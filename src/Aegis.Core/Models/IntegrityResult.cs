namespace Aegis.Core.Models;

/// <summary>
/// Outcome of an integrity check execution.
/// </summary>
public record IntegrityResult(
    long Id,
    DateTimeOffset Timestamp,
    string Component,
    bool Passed,
    string DetailsJson,
    bool Recovered,
    string RecoveryAction
);
