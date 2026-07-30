namespace Aegis.Core.Configuration;

public class ProxyOptions
{
    public const string SectionName = "proxy";

    public bool Enabled { get; set; } = false;
    public int ListenPort { get; set; } = 8080;
    public bool HttpsInspection { get; set; } = false;
    public bool CaInstalled { get; set; } = false;
}
