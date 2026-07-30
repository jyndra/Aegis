namespace Aegis.Core.Models;

public enum KeywordMatchType
{
    Exact,
    WordBoundary,
    Contains
}

public record KeywordRule(
    string Keyword,
    int Weight,
    KeywordMatchType MatchType = KeywordMatchType.WordBoundary
);

public record KeywordPack(
    string Name,
    string Version,
    List<KeywordRule> Rules
);
