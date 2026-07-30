using System.Security.Cryptography;
using System.Text.Json;
using Aegis.Core.Errors;
using Aegis.Core.Interfaces;
using Aegis.Core.Models;
using Aegis.Infrastructure.Storage;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Integrity;

public class IntegrityEngine : IIntegrityEngine
{
    private readonly SqliteStorageService _storageService;
    private readonly ISecurityService _securityService;
    private readonly IHealthReporter _healthReporter;
    private readonly IEventRepository _eventRepo;
    private readonly ITimeProvider _timeProvider;
    private readonly ILogger<IntegrityEngine> _logger;

    public IntegrityEngine(
        SqliteStorageService storageService,
        ISecurityService securityService,
        IHealthReporter healthReporter,
        IEventRepository eventRepo,
        ITimeProvider timeProvider,
        ILogger<IntegrityEngine> logger)
    {
        _storageService = storageService;
        _securityService = securityService;
        _healthReporter = healthReporter;
        _eventRepo = eventRepo;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<IntegrityReport> RunBootAuditAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Aegis Boot-Time Integrity Audit...");
        var checks = new List<IntegrityCheckResult>();

        // 1. Database Integrity Audit (PRAGMA integrity_check)
        bool dbOk = await VerifyDatabaseIntegrityAsync(checks, cancellationToken);

        // 2. HMAC Row Signatures Audit
        bool hmacOk = await VerifyDatabaseRowHmacsAsync(checks, cancellationToken);

        // 3. Critical Policy Files & Manifest Audit
        bool filesOk = await VerifyPolicyAndManifestFilesAsync(checks, cancellationToken);

        // 4. Subsystem Health Baselines Audit
        bool healthOk = await VerifySubsystemHealthAsync(checks, cancellationToken);

        bool overallHealthy = dbOk && hmacOk && filesOk && healthOk;

        if (!overallHealthy)
        {
            _logger.LogWarning("Boot integrity audit identified issues. Triggering self-healing procedures ({ErrorCode})...", AegisErrorCodes.DegradedModeActivated);
            await AttemptSelfHealingAsync("All", cancellationToken);
        }
        else
        {
            _logger.LogInformation("Aegis Boot-Time Integrity Audit passed with 100% health.");
        }

        var report = new IntegrityReport(overallHealthy, checks, _timeProvider.UtcNow);
        await PersistIntegrityReportAsync(report, cancellationToken);

        return report;
    }

    public async Task<IntegrityReport> RunPeriodicAuditAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Running periodic background integrity audit...");
        var checks = new List<IntegrityCheckResult>();

        bool dbOk = await VerifyDatabaseIntegrityAsync(checks, cancellationToken);
        bool hmacOk = await VerifyDatabaseRowHmacsAsync(checks, cancellationToken);

        bool overallHealthy = dbOk && hmacOk;
        var report = new IntegrityReport(overallHealthy, checks, _timeProvider.UtcNow);

        if (!overallHealthy)
        {
            _logger.LogWarning("Periodic audit detected failure! Logging warning event.");
            await _eventRepo.AddEventAsync(new AegisEvent(
                Id: 0,
                Timestamp: _timeProvider.UtcNow,
                Component: "Integrity",
                EventType: "PeriodicAuditFailed",
                Severity: FilterSeverity.Warning,
                Message: "Periodic integrity audit detected database or HMAC mismatch",
                DetailsJson: $"{{\"errorCode\":\"{AegisErrorCodes.StateIntegrityViolated}\"}}"
            ), cancellationToken);
        }

        return report;
    }

    public async Task<bool> AttemptSelfHealingAsync(string component, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting self-healing for component '{Component}'...", component);
        bool healed = false;

        try
        {
            // Self-Healing Action 1: Restore missing default policy JSON files
            string policyDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Aegis", "policies");
            Directory.CreateDirectory(policyDir);

            string kwPath = Path.Combine(policyDir, "keywords-default.json");
            if (!File.Exists(kwPath))
            {
                var defaultKw = new { Name = "DefaultKeywords", Version = "1.0", Rules = new[] { new { Keyword = "porn", Weight = 50, MatchType = "WordBoundary" } } };
                await File.WriteAllTextAsync(kwPath, JsonSerializer.Serialize(defaultKw), cancellationToken);
                _logger.LogInformation("Self-healing restored missing default keyword pack at {Path}", kwPath);
                healed = true;
            }

            string rxPath = Path.Combine(policyDir, "regex-default.json");
            if (!File.Exists(rxPath))
            {
                var defaultRx = new { Name = "DefaultRegex", Version = "1.0", Rules = new[] { new { Pattern = @"\b(porn|porno|xxx)\b", Weight = 80, Category = "ExplicitDomain", Description = "Explicit heuristics" } } };
                await File.WriteAllTextAsync(rxPath, JsonSerializer.Serialize(defaultRx), cancellationToken);
                _logger.LogInformation("Self-healing restored missing default regex pack at {Path}", rxPath);
                healed = true;
            }

            // Self-Healing Action 2: SQLite WAL Checkpoint
            await _storageService.CheckpointWalAsync(cancellationToken);
            _logger.LogInformation("Self-healing executed SQLite WAL checkpoint.");

            await _eventRepo.AddEventAsync(new AegisEvent(
                Id: 0,
                Timestamp: _timeProvider.UtcNow,
                Component: "Integrity",
                EventType: "SelfHealingExecuted",
                Severity: FilterSeverity.Info,
                Message: $"Self-healing executed for component '{component}'",
                DetailsJson: $"{{\"healed\": {healed.ToString().ToLower()}}}"
            ), cancellationToken);

            return healed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Self-healing failed for component '{Component}'", component);
            await _healthReporter.RecordHealthAsync("Service", "Degraded", $"{{\"error\":\"Self-healing failure: {ex.Message}\"}}", cancellationToken);
            return false;
        }
    }

    private async Task<bool> VerifyDatabaseIntegrityAsync(List<IntegrityCheckResult> checks, CancellationToken cancellationToken)
    {
        try
        {
            bool ok = await _storageService.CheckIntegrityAsync(cancellationToken);
            checks.Add(new IntegrityCheckResult(
                CheckType: "SqliteIntegrity",
                Passed: ok,
                Message: ok ? "SQLite PRAGMA integrity_check passed" : "SQLite integrity check failed",
                DetailsJson: $"{{\"sqliteOk\": {ok.ToString().ToLower()}}}"
            ));
            return ok;
        }
        catch (Exception ex)
        {
            checks.Add(new IntegrityCheckResult("SqliteIntegrity", false, ex.Message, "{}"));
            return false;
        }
    }

    private async Task<bool> VerifyDatabaseRowHmacsAsync(List<IntegrityCheckResult> checks, CancellationToken cancellationToken)
    {
        try
        {
            using var connection = _storageService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT locked, lock_started_at, lock_expires_at, stage, failed_attempts, hmac_signature FROM lock_state ORDER BY id DESC LIMIT 1;";

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                bool locked = reader.GetInt32(0) == 1;
                var started = DateTimeOffset.Parse(reader.GetString(1));
                var expires = DateTimeOffset.Parse(reader.GetString(2));
                int stage = reader.GetInt32(3);
                int failed = reader.GetInt32(4);
                string hmac = reader.IsDBNull(5) ? "" : reader.GetString(5);

                string payload = $"{locked}:{started:o}:{expires:o}:{stage}:{failed}";
                bool verified = !string.IsNullOrEmpty(hmac) && _securityService.VerifyRowHmac(payload, hmac);

                checks.Add(new IntegrityCheckResult(
                    CheckType: "LockStateHmac",
                    Passed: verified,
                    Message: verified ? "lock_state row HMAC signature valid" : "lock_state row HMAC signature invalid or missing",
                    DetailsJson: $"{{\"verified\": {verified.ToString().ToLower()}}}"
                ));

                return verified;
            }

            checks.Add(new IntegrityCheckResult("LockStateHmac", true, "lock_state table empty", "{}"));
            return true;
        }
        catch (Exception ex)
        {
            checks.Add(new IntegrityCheckResult("LockStateHmac", false, ex.Message, "{}"));
            return false;
        }
    }

    private Task<bool> VerifyPolicyAndManifestFilesAsync(List<IntegrityCheckResult> checks, CancellationToken cancellationToken)
    {
        string policyDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Aegis", "policies");
        bool dirExists = Directory.Exists(policyDir);

        checks.Add(new IntegrityCheckResult(
            CheckType: "PolicyDirectory",
            Passed: dirExists,
            Message: dirExists ? "Policy directory exists" : "Policy directory missing",
            DetailsJson: $"{{\"path\": \"{policyDir.Replace("\\", "\\\\")}\"}}"
        ));

        return Task.FromResult(dirExists);
    }

    private async Task<bool> VerifySubsystemHealthAsync(List<IntegrityCheckResult> checks, CancellationToken cancellationToken)
    {
        var report = await _healthReporter.GetStatusReportAsync(cancellationToken);
        bool allHealthy = report.All(card => card.Status.Equals("Healthy", StringComparison.OrdinalIgnoreCase));

        checks.Add(new IntegrityCheckResult(
            CheckType: "SubsystemHealth",
            Passed: allHealthy,
            Message: allHealthy ? "All subsystem health cards healthy" : "One or more subsystems in degraded state",
            DetailsJson: JsonSerializer.Serialize(report)
        ));

        return allHealthy;
    }

    private async Task PersistIntegrityReportAsync(IntegrityReport report, CancellationToken cancellationToken)
    {
        try
        {
            using var connection = _storageService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO integrity_checks (check_type, status, details_json, checked_at)
                VALUES ($type, $status, $details, $checked_at);
            ";

            cmd.Parameters.AddWithValue("$type", "BootAudit");
            cmd.Parameters.AddWithValue("$status", report.Healthy ? "Passed" : "Failed");
            cmd.Parameters.AddWithValue("$details", JsonSerializer.Serialize(report.Checks));
            cmd.Parameters.AddWithValue("$checked_at", report.CheckedAt.ToString("o"));

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist integrity audit report to SQLite database.");
        }
    }
}
