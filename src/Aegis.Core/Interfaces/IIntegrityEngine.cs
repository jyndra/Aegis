using Aegis.Core.Models;

namespace Aegis.Core.Interfaces;

/// <summary>
/// Performs component audits, tamper checks, and self-healing.
/// </summary>
public interface IIntegrityEngine
{
    Task<IReadOnlyList<IntegrityResult>> RunAuditAsync(CancellationToken cancellationToken = default);
    Task<bool> AttemptSelfHealingAsync(string component, CancellationToken cancellationToken = default);
}
