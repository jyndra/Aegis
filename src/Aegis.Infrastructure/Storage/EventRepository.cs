using Aegis.Core.Interfaces;
using Aegis.Core.Models;

namespace Aegis.Infrastructure.Storage;

internal class EventRepository : IEventRepository
{
    public Task AddEventAsync(AegisEvent aegisEvent, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<AegisEvent>> GetRecentEventsAsync(int count = 50, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task PurgeExpiredEventsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
