using Aegis.Core.Interfaces;

namespace Aegis.Infrastructure.Rules;

internal class RegexEngine : IRegexEngine
{
    public Task<int> EvaluateRegexScoreAsync(string target, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task ReloadPatternsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
