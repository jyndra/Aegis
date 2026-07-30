namespace Aegis.Core.Configuration;

public class ServiceOptions
{
    public const string SectionName = "service";

    public int ApiPort { get; set; } = 9443;
    public string ApiBindAddress { get; set; } = "127.0.0.1";
    public int HealthCheckIntervalSeconds { get; set; } = 60;
    public int IntegrityCheckIntervalSeconds { get; set; } = 300;
    public string LogLevel { get; set; } = "Information";
    public int LogRetentionDays { get; set; } = 30;
    public int MaxLogSizeMB { get; set; } = 50;
}
