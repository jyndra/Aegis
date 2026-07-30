using Aegis.Core.Models;

namespace Aegis.Core.Interfaces;

/// <summary>
/// Core rule evaluation engine that orchestrates domain, keyword, and heuristic evaluation.
/// </summary>
public interface IRuleEngine
{
    Task<EvaluationResult> EvaluateAsync(EvaluationRequest request, CancellationToken cancellationToken = default);
}
