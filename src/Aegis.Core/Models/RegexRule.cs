namespace Aegis.Core.Models;

public record RegexRule(
    string Pattern,
    int Weight,
    string Category = "General",
    string Description = ""
);

public record RegexPack(
    string Name,
    string Version,
    List<RegexRule> Rules
);
