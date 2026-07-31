using System.Text.Json.Serialization;

namespace Aegis.Core.Models;

/// <summary>
/// Represents the base operational protection state of the Aegis system.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProtectionState
{
    /// <summary>
    /// All critical components are healthy and active.
    /// </summary>
    Protected,

    /// <summary>
    /// One or more critical components are missing or compromised; fail-closed behavior active.
    /// </summary>
    Degraded,

    /// <summary>
    /// Self-healing or automated repair is currently running.
    /// </summary>
    Recovery,

    /// <summary>
    /// Unlock flow initiated after commitment period expiration.
    /// </summary>
    UnlockPending,

    /// <summary>
    /// Protection is disabled after full unlock workflow completion.
    /// </summary>
    Disabled
}
