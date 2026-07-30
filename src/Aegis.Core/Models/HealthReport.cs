namespace Aegis.Core.Models;

/// <summary>
/// Health snapshot of a component or system module.
/// </summary>
public record HealthReport(
    string Component,
    string Status,
    DateTimeOffset LastCheckedAt,
    string DetailJson
);
