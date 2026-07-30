using System.IO;
using Aegis.Core.Interfaces;
using Aegis.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aegis.Infrastructure.Deployment;

public class UninstallerService : IUninstallerService
{
    private readonly ICommitLockEngine _commitLockEngine;
    private readonly ILogger<UninstallerService> _logger;

    public UninstallerService(ICommitLockEngine commitLockEngine, ILogger<UninstallerService>? logger = null)
    {
        _commitLockEngine = commitLockEngine;
        _logger = logger ?? NullLogger<UninstallerService>.Instance;
    }

    public async Task<(bool CanUninstall, string Reason)> CheckCanUninstallAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Verifying commitment device lock status prior to uninstallation authorization...");
        
        var status = await _commitLockEngine.GetStatusAsync(cancellationToken);
        if (status.Locked)
        {
            string blockReason = $"Uninstallation strictly rejected: Active 25-day commitment device lock is running (Stage: {status.Stage}, Expires: {status.LockExpiresAt:u}). Protection cannot be tampered with or removed.";
            _logger.LogWarning("COMMITMENT LOCK REJECTION: {Reason}", blockReason);
            return (false, blockReason);
        }

        return (true, "Commitment device lock is inactive or unlocked. Clean uninstallation authorized.");
    }

    public async Task<UninstallResult> UninstallAsync(string? overrideRootPath = null, bool forceConfirm = false, CancellationToken cancellationToken = default)
    {
        string rootPath = overrideRootPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Aegis");
        _logger.LogInformation("Uninstallation request received for target path: '{Path}' (ForceConfirm={Force})", rootPath, forceConfirm);

        // 1. Enforce Commitment Device Gating
        var (canUninstall, reason) = await CheckCanUninstallAsync(cancellationToken);
        if (!canUninstall)
        {
            return new UninstallResult(
                Success: false,
                Message: reason,
                BlockedByCommitmentDevice: true,
                RemovedFiles: Array.Empty<string>()
            );
        }

        if (!forceConfirm && overrideRootPath == null)
        {
            return new UninstallResult(
                Success: false,
                Message: "Staged uninstallation requires explicit force confirmation flag in production mode.",
                BlockedByCommitmentDevice: false,
                RemovedFiles: Array.Empty<string>()
            );
        }

        var removedFiles = new List<string>();
        try
        {
            if (Directory.Exists(rootPath))
            {
                // Staff Engineer Fix: Robust clean removal with 3-attempt backoff retry loop for temporarily locked handles
                string policyDir = Path.Combine(rootPath, "policies");
                if (Directory.Exists(policyDir))
                {
                    foreach (string file in Directory.GetFiles(policyDir, "*.*", SearchOption.AllDirectories))
                    {
                        bool deleted = false;
                        for (int attempt = 1; attempt <= 3 && !deleted; attempt++)
                        {
                            try
                            {
                                File.Delete(file);
                                removedFiles.Add(file);
                                deleted = true;
                            }
                            catch (IOException ex) when (attempt < 3)
                            {
                                _logger.LogDebug(ex, "File '{File}' temporarily locked. Retrying uninstallation cleanup in 50ms...", file);
                                await Task.Delay(50 * attempt, cancellationToken);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Could not delete file '{File}' during uninstallation after {Attempt} attempts.", file, attempt);
                                break;
                            }
                        }
                    }

                    try { Directory.Delete(policyDir, true); } 
                    catch (Exception ex) { _logger.LogDebug(ex, "Minor cleanup warning removing empty directory '{Dir}'", policyDir); }
                }
            }

            string successMsg = $"Aegis cleanly uninstalled. Reversed {removedFiles.Count} deployed policy/config files from '{rootPath}'.";
            _logger.LogInformation(successMsg);

            return new UninstallResult(
                Success: true,
                Message: successMsg,
                BlockedByCommitmentDevice: false,
                RemovedFiles: removedFiles
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while executing clean uninstallation reverse flow.");
            return new UninstallResult(
                Success: false,
                Message: $"Uninstallation reverse flow error: {ex.Message}",
                BlockedByCommitmentDevice: false,
                RemovedFiles: removedFiles
            );
        }
    }
}
