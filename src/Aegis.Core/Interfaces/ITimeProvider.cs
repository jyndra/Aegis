namespace Aegis.Core.Interfaces;

/// <summary>
/// Time abstraction for wall-clock and monotonic time queries.
/// </summary>
public interface ITimeProvider
{
    DateTimeOffset UtcNow { get; }
    long MonotonicTicks { get; }
    long GetElapsedTimeTicks(long startMonotonicTicks);
}
