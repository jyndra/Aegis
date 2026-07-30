using System.Diagnostics;
using Aegis.Core.Interfaces;

namespace Aegis.Infrastructure.Time;

internal class SystemTimeProvider : ITimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    public long MonotonicTicks => Stopwatch.GetTimestamp();
    public long GetElapsedTimeTicks(long startMonotonicTicks) => Stopwatch.GetTimestamp() - startMonotonicTicks;
}
