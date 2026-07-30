namespace Aegis.Core.Interfaces;

/// <summary>
/// Inspects text queries, titles, and metadata for blacklisted keywords.
/// </summary>
public interface IKeywordEngine
{
    Task<int> MatchScoreAsync(string text, CancellationToken cancellationToken = default);
    Task ReloadKeywordsAsync(CancellationToken cancellationToken = default);
}
