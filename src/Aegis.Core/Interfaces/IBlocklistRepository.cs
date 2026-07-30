using Aegis.Core.Models;

namespace Aegis.Core.Interfaces;

public interface IBlocklistRepository
{
    Task AddDomainAsync(string domain, string source, CancellationToken cancellationToken = default);
    Task BulkAddDomainsAsync(IEnumerable<string> domains, string source, CancellationToken cancellationToken = default);
    Task<bool> ContainsDomainHashAsync(string domainHash, CancellationToken cancellationToken = default);
    Task<HashSet<string>> GetAllDomainHashesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BlockedRule>> GetRulesAsync(CancellationToken cancellationToken = default);
}
