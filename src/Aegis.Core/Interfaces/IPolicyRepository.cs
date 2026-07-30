namespace Aegis.Core.Interfaces;

public interface IPolicyRepository
{
    Task<string?> GetActivePolicyVersionAsync(CancellationToken cancellationToken = default);
    Task SavePolicyVersionAsync(string name, string version, string checksum, string rowHmac, CancellationToken cancellationToken = default);
}
