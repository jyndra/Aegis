namespace Aegis.Core.Models;

public record TextClassificationResult(
    bool IsExplicit,
    double ConfidenceScore,
    string Category,
    string Summary
);
