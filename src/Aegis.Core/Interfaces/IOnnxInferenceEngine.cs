namespace Aegis.Core.Interfaces;

public interface IOnnxInferenceEngine
{
    bool IsModelLoaded { get; }
    Task<double> EvaluateNsfwProbabilityAsync(byte[]? imageBytes, CancellationToken cancellationToken = default);
}
