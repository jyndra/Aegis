using System.Diagnostics;
using Aegis.Core.Errors;
using Aegis.Core.Interfaces;
using Aegis.Core.Models;
using Aegis.Infrastructure.Storage;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Commitment;

public class CommitLockEngine : ICommitLockEngine
{
    private readonly SqliteStorageService _storageService;
    private readonly ISecurityService _securityService;
    private readonly ITimeProvider _timeProvider;
    private readonly IEventRepository _eventRepo;
    private readonly ILogger<CommitLockEngine> _logger;

    private static readonly long InitialMonotonicTicks = Stopwatch.GetTimestamp();
    private static readonly DateTimeOffset InitialUtcTime = DateTimeOffset.UtcNow;

    public CommitLockEngine(
        SqliteStorageService storageService,
        ISecurityService securityService,
        ITimeProvider timeProvider,
        IEventRepository eventRepo,
        ILogger<CommitLockEngine> logger)
    {
        _storageService = storageService;
        _securityService = securityService;
        _timeProvider = timeProvider;
        _eventRepo = eventRepo;
        _logger = logger;
    }

    public async Task<LockState> GetLockStateAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _storageService.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT locked, lock_started_at, lock_expires_at, unlock_requested_at, stage, failed_attempts, updated_at, hmac_signature
            FROM lock_state
            ORDER BY id DESC LIMIT 1;
        ";

        bool hasData = false;
        bool locked = false;
        DateTimeOffset lockStartedAt = DateTimeOffset.MinValue;
        DateTimeOffset lockExpiresAt = DateTimeOffset.MinValue;
        DateTimeOffset? unlockRequestedAt = null;
        int stage = 0;
        int failedAttempts = 0;
        DateTimeOffset updatedAt = DateTimeOffset.MinValue;
        string hmac = "";

        using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                hasData = true;
                locked = reader.GetInt32(0) == 1;
                lockStartedAt = DateTimeOffset.Parse(reader.GetString(1));
                lockExpiresAt = DateTimeOffset.Parse(reader.GetString(2));
                unlockRequestedAt = reader.IsDBNull(3) ? null : DateTimeOffset.Parse(reader.GetString(3));
                stage = reader.GetInt32(4);
                failedAttempts = reader.GetInt32(5);
                updatedAt = DateTimeOffset.Parse(reader.GetString(6));
                hmac = reader.IsDBNull(7) ? "" : reader.GetString(7);
            }
        }

        if (hasData)
        {
            // HMAC Integrity Verification
            string payload = $"{locked}:{lockStartedAt:o}:{lockExpiresAt:o}:{stage}:{failedAttempts}";
            if (!string.IsNullOrEmpty(hmac) && !_securityService.VerifyRowHmac(payload, hmac))
            {
                _logger.LogError("CRITICAL: lock_state table HMAC verification failed! Tampering detected ({ErrorCode}).", AegisErrorCodes.StateIntegrityViolated);
                await _eventRepo.AddEventAsync(new AegisEvent(
                    Id: 0,
                    Timestamp: _timeProvider.UtcNow,
                    Component: "CommitLock",
                    EventType: "LockStateTampered",
                    Severity: FilterSeverity.Critical,
                    Message: "Database lock_state table row failed HMAC signature check",
                    DetailsJson: $"{{\"errorCode\":\"{AegisErrorCodes.StateIntegrityViolated}\"}}"
                ), cancellationToken);
            }

            // Anti-Clock-Tampering Monotonic Check (with automatic Expiration extension)
            lockExpiresAt = await VerifyAndAdjustMonotonicClockSafetyAsync(locked, lockStartedAt, lockExpiresAt, unlockRequestedAt, stage, failedAttempts, cancellationToken);

            return new LockState(locked, lockStartedAt, lockExpiresAt, unlockRequestedAt, stage, failedAttempts, updatedAt);
        }

        // Initialize default 25-day lock state if empty
        return await LockAsync(25, cancellationToken);
    }

    public async Task<LockState> LockAsync(int durationDays = 25, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.UtcNow;
        var expiresAt = now.AddDays(durationDays);

        string payload = $"true:{now:o}:{expiresAt:o}:0:0";
        string hmac = _securityService.ComputeRowHmac(payload);

        using var connection = _storageService.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO lock_state (locked, lock_started_at, lock_expires_at, unlock_requested_at, stage, failed_attempts, updated_at, hmac_signature)
            VALUES (1, $started_at, $expires_at, NULL, 0, 0, $updated_at, $hmac);
        ";
        cmd.Parameters.AddWithValue("$started_at", now.ToString("o"));
        cmd.Parameters.AddWithValue("$expires_at", expiresAt.ToString("o"));
        cmd.Parameters.AddWithValue("$updated_at", now.ToString("o"));
        cmd.Parameters.AddWithValue("$hmac", hmac);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation("Commitment lock engaged for {Days} days (expires {ExpiresAt})", durationDays, expiresAt);

        return new LockState(true, now, expiresAt, null, 0, 0, now);
    }

    public async Task<UnlockProgress> InitiateUnlockStageAsync(int targetStage, string? confirmationChallenge = null, CancellationToken cancellationToken = default)
    {
        var state = await GetLockStateAsync(cancellationToken);
        var now = _timeProvider.UtcNow;

        if (state.FailedAttempts >= 3)
        {
            _logger.LogWarning("Unlock attempt blocked due to rate limiting ({Failed} failed attempts)", state.FailedAttempts);
            return new UnlockProgress(false, state.UnlockStage, "Maximum failed unlock attempts reached. Lockout active for 24 hours.", TimeSpan.FromHours(24));
        }

        if (!state.Locked || now >= state.LockExpiresAt)
        {
            return new UnlockProgress(true, 3, "Commitment timer has naturally expired. Protection unlocked.", TimeSpan.Zero);
        }

        // Stage 1: Request Unlock & Start 48h Cooldown
        if (targetStage == 1)
        {
            await SaveLockStateUpdateAsync(state.Locked, state.LockStartedAt, state.LockExpiresAt, now, 1, state.FailedAttempts, cancellationToken);
            _logger.LogInformation("Stage 1 unlock initiated. 48-hour mandatory cooling-off period started.");

            return new UnlockProgress(true, 1, "Stage 1 initiated. 48-hour mandatory cooling-off period started.", TimeSpan.FromHours(48));
        }

        // Stage 2: Confirmation after 48h Cooldown
        if (targetStage == 2)
        {
            if (state.UnlockRequestedAt == null)
            {
                return new UnlockProgress(false, 0, "Stage 1 unlock request must be initiated first.", null);
            }

            var elapsedCooldown = now - state.UnlockRequestedAt.Value;
            if (elapsedCooldown < TimeSpan.FromHours(48))
            {
                var remaining = TimeSpan.FromHours(48) - elapsedCooldown;
                _logger.LogWarning("Stage 2 unlock rejected: Cooling-off period incomplete ({Remaining} left)", remaining);
                return new UnlockProgress(false, 1, $"Cooling-off period incomplete. Please wait {remaining.Hours}h {remaining.Minutes}m.", remaining);
            }

            if (confirmationChallenge != "CONFIRM_UNLOCK_AEGIS")
            {
                int newFailCount = state.FailedAttempts + 1;
                await SaveLockStateUpdateAsync(state.Locked, state.LockStartedAt, state.LockExpiresAt, state.UnlockRequestedAt, state.UnlockStage, newFailCount, cancellationToken);
                _logger.LogWarning("Stage 2 unlock failed: Invalid confirmation passphrase");

                return new UnlockProgress(false, 1, "Invalid confirmation passphrase. Failed attempt recorded.", null);
            }

            await SaveLockStateUpdateAsync(state.Locked, state.LockStartedAt, state.LockExpiresAt, state.UnlockRequestedAt, 2, state.FailedAttempts, cancellationToken);
            return new UnlockProgress(true, 2, "Stage 2 confirmation accepted. Submit final Stage 3 confirmation to unlock.", TimeSpan.Zero);
        }

        // Stage 3: Final Unlock Confirmation
        if (targetStage == 3)
        {
            if (state.UnlockStage < 2)
            {
                return new UnlockProgress(false, state.UnlockStage, "Stage 2 confirmation required prior to final unlock.", null);
            }

            await SaveLockStateUpdateAsync(false, state.LockStartedAt, now, state.UnlockRequestedAt, 3, 0, cancellationToken);
            _logger.LogInformation("Stage 3 completed. Commitment lock successfully unlocked.");

            return new UnlockProgress(true, 3, "Commitment lock successfully unlocked.", TimeSpan.Zero);
        }

        return new UnlockProgress(false, state.UnlockStage, "Invalid unlock stage requested", null);
    }

    public async Task<bool> IsUnlockAllowedAsync(CancellationToken cancellationToken = default)
    {
        var state = await GetLockStateAsync(cancellationToken);
        return !state.Locked || _timeProvider.UtcNow >= state.LockExpiresAt;
    }

    public async Task<CommitLockStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var state = await GetLockStateAsync(cancellationToken);
        var now = _timeProvider.UtcNow;
        var unlockStage = (UnlockStage)Math.Min(3, Math.Max(0, state.UnlockStage));
        double secsRemaining = Math.Max(0, (state.LockExpiresAt - now).TotalSeconds);

        return new CommitLockStatus(
            Locked: state.Locked,
            Stage: unlockStage,
            StageChangedAt: state.UpdatedAt,
            LockExpiresAt: state.LockExpiresAt,
            CanAdvance: !state.Locked || now >= state.LockExpiresAt,
            SecondsRemainingInStage: (long)secsRemaining,
            NextStageAvailableAt: state.UnlockRequestedAt?.AddHours(48)
        );
    }

    public async Task ResetFailedAttemptsAsync(CancellationToken cancellationToken = default)
    {
        var state = await GetLockStateAsync(cancellationToken);
        await SaveLockStateUpdateAsync(state.Locked, state.LockStartedAt, state.LockExpiresAt, state.UnlockRequestedAt, state.UnlockStage, 0, cancellationToken);
    }

    private async Task SaveLockStateUpdateAsync(
        bool locked,
        DateTimeOffset lockStartedAt,
        DateTimeOffset lockExpiresAt,
        DateTimeOffset? unlockRequestedAt,
        int stage,
        int failedAttempts,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.UtcNow;
        string payload = $"{locked}:{lockStartedAt:o}:{lockExpiresAt:o}:{stage}:{failedAttempts}";
        string hmac = _securityService.ComputeRowHmac(payload);

        using var connection = _storageService.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO lock_state (locked, lock_started_at, lock_expires_at, unlock_requested_at, stage, failed_attempts, updated_at, hmac_signature)
            VALUES ($locked, $started_at, $expires_at, $requested_at, $stage, $failed, $updated_at, $hmac);
        ";

        AddParameter(cmd, "$locked", locked ? 1 : 0);
        AddParameter(cmd, "$started_at", lockStartedAt.ToString("o"));
        AddParameter(cmd, "$expires_at", lockExpiresAt.ToString("o"));
        AddParameter(cmd, "$requested_at", (object?)unlockRequestedAt?.ToString("o") ?? DBNull.Value);
        AddParameter(cmd, "$stage", stage);
        AddParameter(cmd, "$failed", failedAttempts);
        AddParameter(cmd, "$updated_at", now.ToString("o"));
        AddParameter(cmd, "$hmac", hmac);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<DateTimeOffset> VerifyAndAdjustMonotonicClockSafetyAsync(
        bool locked,
        DateTimeOffset lockStartedAt,
        DateTimeOffset lockExpiresAt,
        DateTimeOffset? unlockRequestedAt,
        int stage,
        int failedAttempts,
        CancellationToken cancellationToken)
    {
        if (!locked) return lockExpiresAt;

        long currentTicks = Stopwatch.GetTimestamp();
        double elapsedMonotonicSec = (currentTicks - InitialMonotonicTicks) / (double)Stopwatch.Frequency;

        DateTimeOffset expectedMinUtc = InitialUtcTime.AddSeconds(elapsedMonotonicSec);
        var currentUtc = _timeProvider.UtcNow;
        double driftHours = (currentUtc - expectedMinUtc).TotalHours;

        // If system UTC time jumped forward >1 hour ahead of monotonic tick elapsed time
        if (driftHours > 1)
        {
            _logger.LogWarning("System clock manipulation detected! UTC jumped {Hours:F2}h forward ahead of monotonic ticks ({ErrorCode}). Extending expiration.",
                driftHours, AegisErrorCodes.ClockManipulationDetected);

            // Penalize/adjust lock expiration by extending it by the drift amount
            var adjustedExpiresAt = lockExpiresAt.AddHours(driftHours);
            await SaveLockStateUpdateAsync(locked, lockStartedAt, adjustedExpiresAt, unlockRequestedAt, stage, failedAttempts, cancellationToken);

            await _eventRepo.AddEventAsync(new AegisEvent(
                Id: 0,
                Timestamp: currentUtc,
                Component: "CommitLock",
                EventType: "ClockManipulationDetected",
                Severity: FilterSeverity.Warning,
                Message: $"Clock jump detected ({driftHours:F2}h drift). Lock timer extended to {adjustedExpiresAt:o}.",
                DetailsJson: $"{{\"errorCode\":\"{AegisErrorCodes.ClockManipulationDetected}\", \"driftHours\": {driftHours}}}"
            ), cancellationToken);

            return adjustedExpiresAt;
        }

        return lockExpiresAt;
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var param = command.CreateParameter();
        param.ParameterName = name;
        param.Value = value;
        command.Parameters.Add(param);
    }
}
