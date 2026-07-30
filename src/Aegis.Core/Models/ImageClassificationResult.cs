namespace Aegis.Core.Models;

public record ImageClassificationResult(
    bool IsExplicit,
    double SkinTonePercentage,
    double NsfwProbability,
    string Category
);
