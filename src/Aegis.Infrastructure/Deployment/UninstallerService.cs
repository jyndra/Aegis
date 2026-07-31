using System.IO;
using Aegis.Core.Configuration;
using Aegis.Core.Interfaces;
using Aegis.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.Deployment;

public class UninstallerService : IUninstallerService
{
    private readonly ICommitLockEngine _commitLockEngine;
    private readonly IOptions<LockOptions> _lockOptions;
    private readonly ILogger<UninstallerService> _logger;

    private static int _currentStep = 0;
    private static DateTimeOffset _lastStepCompletedAt = DateTimeOffset.MinValue;
    private static readonly object _stepLock = new();

    public static readonly TimeSpan StepCooldown = TimeSpan.FromMinutes(5);

    public UninstallerService(
        ICommitLockEngine commitLockEngine,
        IOptions<LockOptions>? lockOptions = null,
        ILogger<UninstallerService>? logger = null)
    {
        _commitLockEngine = commitLockEngine;
        _lockOptions = lockOptions ?? Options.Create(new LockOptions());
        _logger = logger ?? NullLogger<UninstallerService>.Instance;
    }

    public async Task<(bool CanUninstall, string Reason)> CheckCanUninstallAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Verifying commitment device lock status prior to uninstallation authorization...");

        // Test Mode Bypass Check
        if (_lockOptions.Value.BypassLockForTesting)
        {
            _logger.LogInformation("TEST MODE ACTIVE: Commitment lock bypassed for testing purposes.");
            return (true, "Test mode active: Uninstallation authorized.");
        }

        var status = await _commitLockEngine.GetStatusAsync(cancellationToken);
        if (status.Locked)
        {
            string blockReason = $"Uninstallation strictly rejected: Active 25-day commitment device lock is running (Stage: {status.Stage}, Expires: {status.LockExpiresAt:u}). Protection cannot be tampered with or removed.";
            _logger.LogWarning("COMMITMENT LOCK REJECTION: {Reason}", blockReason);
            return (false, blockReason);
        }

        return (true, "Commitment device lock is inactive or unlocked. Clean uninstallation authorized.");
    }

    public async Task<UninstallResult> UninstallAsync(
        string? overrideRootPath = null,
        bool forceConfirm = false,
        int confirmationStep = 1,
        CancellationToken cancellationToken = default)
    {
        string rootPath = overrideRootPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Aegis");
        _logger.LogInformation("Uninstallation request received for target path: '{Path}' (ForceConfirm={Force}, Step={Step}/10)", rootPath, forceConfirm, confirmationStep);

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

        // 2. Enforce 10-Step Interactive Challenge with 5-Minute Cooldown ONLY in production mode (bypassed in test mode or custom test root)
        if (!_lockOptions.Value.BypassLockForTesting && overrideRootPath == null)
        {
            lock (_stepLock)
            {
                var now = DateTimeOffset.UtcNow;

                if (confirmationStep == 1)
                {
                    _currentStep = 1;
                    _lastStepCompletedAt = now;
                    string msg1 = "Step 1/10 confirmed! Mandatory 5-minute cooling-off period started. Please wait 5 minutes before submitting step=2.";
                    _logger.LogInformation(msg1);
                    return new UninstallResult(false, msg1, false, Array.Empty<string>());
                }

                if (confirmationStep != _currentStep + 1)
                {
                    string seqMsg = $"Invalid uninstallation sequence. Currently at step {_currentStep}/10. Please submit step={_currentStep + 1}.";
                    _logger.LogWarning(seqMsg);
                    return new UninstallResult(false, seqMsg, false, Array.Empty<string>());
                }

                var elapsed = now - _lastStepCompletedAt;
                if (elapsed < StepCooldown)
                {
                    var remaining = StepCooldown - elapsed;
                    string cooldownMsg = $"Step {confirmationStep} Cooldown Active: Please wait {remaining.Minutes}m {remaining.Seconds}s before confirming step {confirmationStep} (Mandatory 5-minute cooling-off period per step).";
                    _logger.LogWarning(cooldownMsg);
                    return new UninstallResult(false, cooldownMsg, false, Array.Empty<string>());
                }

                // Cooldown satisfied — advance to requested step
                _currentStep = confirmationStep;
                _lastStepCompletedAt = now;

                if (confirmationStep < 10)
                {
                    string nextMsg = $"Step {confirmationStep}/10 confirmed! Mandatory 5-minute cooling-off period started. Please wait 5 minutes before submitting step={confirmationStep + 1}.";
                    _logger.LogInformation(nextMsg);
                    return new UninstallResult(false, nextMsg, false, Array.Empty<string>());
                }
            }
        }

        // Reaching Step 10 satisfies the complete 10-step 50-minute challenge flow
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
                // Clean teardown with 3-attempt backoff retry loop
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

            // Reset step tracker upon clean teardown completion
            lock (_stepLock) { _currentStep = 0; }

            string successMsg = $"Aegis cleanly uninstalled (10/10 steps confirmed with 50 minutes cumulative cooling-off). Reversed {removedFiles.Count} deployed policy/config files from '{rootPath}'.";
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
