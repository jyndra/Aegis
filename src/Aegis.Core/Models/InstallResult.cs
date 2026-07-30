namespace Aegis.Core.Models;

public record InstallResult(
    bool Success,
    string Message,
    string InstallRootPath,
    IReadOnlyList<string> DeployedFiles
);
