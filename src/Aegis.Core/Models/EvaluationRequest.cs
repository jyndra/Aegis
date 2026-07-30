namespace Aegis.Core.Models;

/// <summary>
/// Evaluation payload submitted to rule engine.
/// </summary>
public record EvaluationRequest(
    string Url,
    string? Domain,
    string? Path,
    string? Query,
    string? Title,
    string? Referrer,
    string? Browser,
    string Component,
    DateTimeOffset Timestamp
);
