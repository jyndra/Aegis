namespace Aegis.Core.Models;

public record UninstallResult(
    bool Success,
    string Message,
    bool BlockedByCommitmentDevice,
    IReadOnlyList<string> RemovedFiles
);
