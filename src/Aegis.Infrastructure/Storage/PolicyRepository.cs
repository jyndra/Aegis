using Aegis.Core.Interfaces;

namespace Aegis.Infrastructure.Storage;

internal class PolicyRepository : IPolicyRepository
{
    public Task<string?> GetActivePolicyVersionAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task SavePolicyVersionAsync(string name, string version, string checksum, string rowHmac, CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
