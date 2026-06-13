namespace Moongazing.Orion.Abstractions.Testing;

using Moongazing.Orion.Abstractions.Time;

/// <summary>
/// A deterministic <see cref="IOrionClock"/> for tests. The wall clock and the monotonic
/// timestamp are both frozen at construction and only move when <see cref="Advance(TimeSpan)"/>
/// is called, so a test can drive lease expiry, grace periods, and scheduled work without
/// real delays or flakiness.
/// </summary>
public sealed class FrozenOrionClock : IOrionClock
{
    private static readonly long TicksPerTimestamp = System.Diagnostics.Stopwatch.Frequency;
    private DateTimeOffset utcNow;
    private long timestamp;

    /// <summary>Create a frozen clock at <paramref name="start"/> (default: 2026-01-01Z).</summary>
    /// <param name="start">The initial UTC instant.</param>
    public FrozenOrionClock(DateTimeOffset? start = null)
    {
        utcNow = start ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        timestamp = 0;
    }

    /// <inheritdoc />
    public DateTimeOffset UtcNow => utcNow;

    /// <inheritdoc />
    public long GetTimestamp() => timestamp;

    /// <inheritdoc />
    public TimeSpan GetElapsedTime(long startingTimestamp)
        => TimeSpan.FromSeconds((timestamp - startingTimestamp) / (double)TicksPerTimestamp);

    /// <summary>
    /// Advance BOTH the wall clock and the monotonic timestamp by <paramref name="delta"/>.
    /// Negative deltas are rejected (a monotonic clock never goes backward).
    /// </summary>
    /// <param name="delta">The amount to advance.</param>
    public void Advance(TimeSpan delta)
    {
        if (delta < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delta), delta, "A clock cannot advance by a negative amount.");
        }
        utcNow = utcNow.Add(delta);
        timestamp += (long)(delta.TotalSeconds * TicksPerTimestamp);
    }

    /// <summary>Set the wall clock to an explicit instant without moving the monotonic timestamp.</summary>
    /// <param name="value">The new UTC instant (must not be earlier than the current one).</param>
    public void SetUtcNow(DateTimeOffset value)
    {
        if (value < utcNow)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "The wall clock cannot be set earlier than its current value.");
        }
        utcNow = value;
    }
}
