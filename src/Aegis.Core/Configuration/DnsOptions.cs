namespace Aegis.Core.Configuration;

public class DnsOptions
{
    public const string SectionName = "dns";

    public bool Enabled { get; set; } = true;
    public string ListenAddress { get; set; } = "127.0.0.1";
    public int ListenPort { get; set; } = 5354;
    public List<string> UpstreamServers { get; set; } = new() { "1.1.1.1", "8.8.8.8" };
    public int CacheMaxEntries { get; set; } = 10000;
    public int CacheTTLSeconds { get; set; } = 300;
}
