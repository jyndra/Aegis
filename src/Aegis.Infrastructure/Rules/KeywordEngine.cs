using Aegis.Core.Interfaces;

namespace Aegis.Infrastructure.Rules;

internal class KeywordEngine : IKeywordEngine
{
    public Task<int> MatchScoreAsync(string text, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task ReloadKeywordsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
