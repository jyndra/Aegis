using Aegis.Core.Interfaces;
using Aegis.Core.Models;

namespace Aegis.Infrastructure.Storage;

internal class BlocklistRepository : IBlocklistRepository
{
    public Task AddDomainAsync(string domain, string source, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task BulkAddDomainsAsync(IEnumerable<string> domains, string source, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<bool> ContainsDomainHashAsync(string domainHash, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<HashSet<string>> GetAllDomainHashesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<BlockedRule>> GetRulesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
