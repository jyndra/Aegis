namespace Aegis.Core.Configuration;

public class ProxyOptions
{
    public const string SectionName = "Proxy";

    public bool Enabled { get; set; } = true;
    public int ListenPort { get; set; } = 19080;
    public string ListenAddress { get; set; } = "127.0.0.1";
    public bool InterceptHttps { get; set; } = false;
    public string? UpstreamProxy { get; set; } = null;
}
