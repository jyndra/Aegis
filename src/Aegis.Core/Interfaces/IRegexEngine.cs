namespace Aegis.Core.Interfaces;

/// <summary>
/// Evaluates URLs, titles, and domain tokens against regular expression heuristics.
/// </summary>
public interface IRegexEngine
{
    Task<int> EvaluateRegexScoreAsync(string target, CancellationToken cancellationToken = default);
    Task ReloadPatternsAsync(CancellationToken cancellationToken = default);
}
