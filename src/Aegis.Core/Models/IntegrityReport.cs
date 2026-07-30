namespace Aegis.Core.Models;

public record IntegrityCheckResult(
    string CheckType,
    bool Passed,
    string Message,
    string DetailsJson
);

public record IntegrityReport(
    bool Healthy,
    List<IntegrityCheckResult> Checks,
    DateTimeOffset CheckedAt
);
