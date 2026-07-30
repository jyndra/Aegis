namespace Aegis.Core.Interfaces;

public interface IWatchdogClient
{
    Task SendHeartbeatAsync(CancellationToken cancellationToken = default);
    Task<bool> IsWatchdogRunningAsync(CancellationToken cancellationToken = default);
}
