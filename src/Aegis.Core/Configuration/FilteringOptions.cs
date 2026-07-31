namespace Aegis.Core.Configuration;

public class FilteringOptions
{
    public const string SectionName = "filtering";

    public List<string> BlocklistSources { get; set; } = new();
    public string CustomBlacklistPath { get; set; } = "policies/custom-blacklist.txt";
    public List<string> KeywordPackPaths { get; set; } = new() { "policies/keywords-default.json" };
    public List<string> RegexPackPaths { get; set; } = new() { "policies/regex-default.json" };
    public int RuleEvaluationTimeoutMs { get; set; } = 1000;
    public int ScoreThreshold { get; set; } = 70;
}
