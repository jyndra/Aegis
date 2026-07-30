namespace Aegis.Core.Models;

public record InstallOptions(
    string? OverrideInstallPath = null,
    bool RegisterWindowsService = true,
    bool DeployDefaultPolicies = true
)
{
    public string GetResolvedInstallPath() =>
        !string.IsNullOrWhiteSpace(OverrideInstallPath)
            ? OverrideInstallPath
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Aegis");
}
