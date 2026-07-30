namespace Aegis.Core.Models;

/// <summary>
/// Audit trail event record.
/// </summary>
public record AegisEvent(
    long Id,
    DateTimeOffset Timestamp,
    string Component,
    string EventType,
    FilterSeverity Severity,
    string Message,
    string DetailsJson
);
