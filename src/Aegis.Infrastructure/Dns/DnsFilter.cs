using Aegis.Core.Interfaces;

namespace Aegis.Infrastructure.Dns;

internal class DnsFilter : IDnsFilter
{
    public Task StartAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task StopAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<bool> IsDomainBlockedAsync(string domain, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task ReloadBlocklistAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
