using Aegis.Core.Interfaces;
using Aegis.Core.Models;

namespace Aegis.Infrastructure.Integrity;

internal class IntegrityEngine : IIntegrityEngine
{
    public Task<IReadOnlyList<IntegrityResult>> RunAuditAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<bool> AttemptSelfHealingAsync(string component, CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
