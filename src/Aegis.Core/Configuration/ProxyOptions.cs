namespace Aegis.Core.Configuration;

public class ProxyOptions
{
    public const string SectionName = "proxy";

    public bool Enabled { get; set; } = false;
    public int ListenPort { get; set; } = 8081;
    public string ListenAddress { get; set; } = "127.0.0.1";
    public bool InterceptHttps { get; set; } = false;
    public string? UpstreamProxy { get; set; } = null;
}
