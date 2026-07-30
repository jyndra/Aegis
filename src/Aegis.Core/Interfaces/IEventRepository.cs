using Aegis.Core.Models;

namespace Aegis.Core.Interfaces;

public interface IEventRepository
{
    Task AddEventAsync(AegisEvent aegisEvent, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AegisEvent>> GetRecentEventsAsync(int count = 50, CancellationToken cancellationToken = default);
    Task PurgeExpiredEventsAsync(CancellationToken cancellationToken = default);
}
