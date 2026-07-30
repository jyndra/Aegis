using Aegis.Core.Models;

namespace Aegis.Core.Interfaces;

/// <summary>
/// Controls local DNS resolution and domain blocklist filtering.
/// </summary>
public interface IDnsFilter
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task<bool> IsDomainBlockedAsync(string domain, CancellationToken cancellationToken = default);
    Task ReloadBlocklistAsync(CancellationToken cancellationToken = default);
}
