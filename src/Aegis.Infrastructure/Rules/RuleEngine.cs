using Aegis.Core.Interfaces;
using Aegis.Core.Models;

namespace Aegis.Infrastructure.Rules;

internal class RuleEngine : IRuleEngine
{
    public Task<EvaluationResult> EvaluateAsync(EvaluationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
