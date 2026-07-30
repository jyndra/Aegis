using System.IO;
using System.Text.Json;
using Aegis.Core.Interfaces;
using Aegis.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aegis.Infrastructure.Deployment;

public class InstallerService : IInstallerService
{
    private readonly IStorageService _storageService;
    private readonly ILogger<InstallerService> _logger;

    public InstallerService(IStorageService storageService, ILogger<InstallerService>? logger = null)
    {
        _storageService = storageService;
        _logger = logger ?? NullLogger<InstallerService>.Instance;
    }

    public async Task<InstallResult> InstallAsync(InstallOptions options, CancellationToken cancellationToken = default)
    {
        string installRoot = options.GetResolvedInstallPath();
        _logger.LogInformation("Starting Aegis protection installation at root: {Path}", installRoot);

        var deployedFiles = new List<string>();

        try
        {
            // 1. Create directory structure
            string[] subDirs = ["policies", "models", "logs", "backups", "data"];
            foreach (string dir in subDirs)
            {
                string dirPath = Path.Combine(installRoot, dir);
                if (!Directory.Exists(dirPath))
                {
                    Directory.CreateDirectory(dirPath);
                    _logger.LogDebug("Created installation subdirectory: {Dir}", dir);
                }
            }

            // 2. Deploy default policies if requested (Staff Engineer Fix: overwrite=false to preserve user custom policy rules on upgrades)
            if (options.DeployDefaultPolicies)
            {
                var policyFiles = await DeployPoliciesInternalAsync(installRoot, overwrite: false, cancellationToken);
                deployedFiles.AddRange(policyFiles);
            }

            // 3. Initialize SQLite database & migrations
            _logger.LogInformation("Initializing database schema via Storage Service...");
            await _storageService.InitializeDatabaseAsync(cancellationToken);

            string message = $"Aegis successfully installed to '{installRoot}' with {deployedFiles.Count} policy files deployed.";
            _logger.LogInformation(message);

            return new InstallResult(
                Success: true,
                Message: message,
                InstallRootPath: installRoot,
                DeployedFiles: deployedFiles
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete Aegis installation at '{Path}'", installRoot);
            return new InstallResult(
                Success: false,
                Message: $"Installation failed: {ex.Message}",
                InstallRootPath: installRoot,
                DeployedFiles: deployedFiles
            );
        }
    }

    public async Task<bool> RestorePoliciesAsync(string? overrideRootPath = null, CancellationToken cancellationToken = default)
    {
        string targetRoot = overrideRootPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Aegis");
        try
        {
            // Explicit policy restoration forces overwrite=true to repair damaged configuration
            var files = await DeployPoliciesInternalAsync(targetRoot, overwrite: true, cancellationToken);
            _logger.LogInformation("Successfully restored {Count} default policy files to '{Path}'.", files.Count, targetRoot);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore default policies to '{Path}'.", targetRoot);
            return false;
        }
    }

    private async Task<List<string>> DeployPoliciesInternalAsync(string rootPath, bool overwrite, CancellationToken cancellationToken)
    {
        string policyDir = Path.Combine(rootPath, "policies");
        Directory.CreateDirectory(policyDir);

        var deployed = new List<string>();

        string keywordsPath = Path.Combine(policyDir, "keywords-default.json");
        string regexPath = Path.Combine(policyDir, "regex-default.json");

        string defaultKeywordsJson = JsonSerializer.Serialize(new
        {
            version = "1.0.0",
            description = "Default adult explicit search and title keyword triggers",
            keywords = new[] { "porn", "xxx", "nudes", "hentai", "camgirl", "onlyfans", "erotic" }
        }, new JsonSerializerOptions { WriteIndented = true });

        string defaultRegexJson = JsonSerializer.Serialize(new
        {
            version = "1.0.0",
            description = "Default adult URL parameter and hostname heuristic regex rules",
            rules = new[]
            {
                new { pattern = @"\b(porn|xxx|nude|sex)\b", score = 60, description = "Explicit vocabulary token" },
                new { pattern = @"(\.xxx|\.adult|pornhub|xvideos|xhamster)", score = 100, description = "Explicit adult hosting domain" }
            }
        }, new JsonSerializerOptions { WriteIndented = true });

        if (overwrite || !File.Exists(keywordsPath))
        {
            await File.WriteAllTextAsync(keywordsPath, defaultKeywordsJson, cancellationToken);
            deployed.Add(keywordsPath);
        }
        else
        {
            _logger.LogDebug("Preserving existing custom keyword policy file at '{Path}'", keywordsPath);
        }

        if (overwrite || !File.Exists(regexPath))
        {
            await File.WriteAllTextAsync(regexPath, defaultRegexJson, cancellationToken);
            deployed.Add(regexPath);
        }
        else
        {
            _logger.LogDebug("Preserving existing custom regex policy file at '{Path}'", regexPath);
        }

        return deployed;
    }
}
