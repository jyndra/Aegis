using Aegis.Core.Models;

namespace Aegis.Core.Interfaces;

public interface IIntegrityEngine
{
    Task<IntegrityReport> RunBootAuditAsync(CancellationToken cancellationToken = default);
    Task<IntegrityReport> RunPeriodicAuditAsync(CancellationToken cancellationToken = default);
    Task<bool> AttemptSelfHealingAsync(string component, CancellationToken cancellationToken = default);
}
