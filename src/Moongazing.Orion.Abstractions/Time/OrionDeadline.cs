namespace Moongazing.Orion.Abstractions.Time;

using System;

/// <summary>
/// A time budget expressed as a monotonic deadline against an <see cref="IOrionClock"/>: the instant
/// an operation should stop waiting, checkable without subtracting wall-clock values. It uses the
/// clock's monotonic timestamp (<see cref="IOrionClock.GetTimestamp"/> /
/// <see cref="IOrionClock.GetElapsedTime(long)"/>), so it is immune to wall-clock adjustments and
/// stays deterministic under <c>FrozenOrionClock</c> — advance the clock to expire it, no real wait.
/// <para>
/// It replaces the ad-hoc "start a <see cref="System.Diagnostics.Stopwatch"/> and compare Elapsed" or
/// "<see cref="System.Threading.CancellationTokenSource"/>.CancelAfter" patterns that scatter across
/// the family's acquire/retry/renew loops, giving them one testable shape. The originating clock is
/// captured at creation, so <see cref="IsExpired"/> / <see cref="Remaining"/> always evaluate against
/// the right clock — there is no way to check a deadline against an unrelated clock's timeline.
/// </para>
/// </summary>
public readonly struct OrionDeadline : IEquatable<OrionDeadline>
{
    private readonly IOrionClock? clock;
    private readonly long startTimestamp;
    private readonly TimeSpan budget;

    private OrionDeadline(IOrionClock clock, long startTimestamp, TimeSpan budget)
    {
        this.clock = clock;
        this.startTimestamp = startTimestamp;
        this.budget = budget;
    }

    /// <summary>A deadline that never expires — the "no timeout" case. <see cref="IsExpired"/> is always false.</summary>
    public static OrionDeadline Never => default;

    /// <summary>
    /// A deadline <paramref name="budget"/> from now, measured on (and bound to) <paramref name="clock"/>'s
    /// monotonic timestamp. A non-positive budget yields an already-expired deadline. A budget of
    /// <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> yields <see cref="Never"/>.
    /// </summary>
    /// <param name="clock">The clock the deadline is measured on and bound to.</param>
    /// <param name="budget">The time allowed from now.</param>
    /// <returns>The deadline.</returns>
    public static OrionDeadline After(IOrionClock clock, TimeSpan budget)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (budget == System.Threading.Timeout.InfiniteTimeSpan)
        {
            return Never;
        }
        var clamped = budget < TimeSpan.Zero ? TimeSpan.Zero : budget;
        return new OrionDeadline(clock, clock.GetTimestamp(), clamped);
    }

    /// <summary>True when this deadline carries a finite budget; false for <see cref="Never"/>.</summary>
    public bool HasBudget => clock is not null;

    /// <summary>
    /// Whether the deadline has passed, measured on the clock it was created from. Always false for
    /// <see cref="Never"/>.
    /// </summary>
    public bool IsExpired => clock is not null && clock.GetElapsedTime(startTimestamp) >= budget;

    /// <summary>
    /// The time left before the deadline, clamped to zero once passed, measured on the clock it was
    /// created from. Returns <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> for <see cref="Never"/>.
    /// </summary>
    public TimeSpan Remaining
    {
        get
        {
            if (clock is null)
            {
                return System.Threading.Timeout.InfiniteTimeSpan;
            }
            var remaining = budget - clock.GetElapsedTime(startTimestamp);
            return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        }
    }

    /// <inheritdoc />
    public bool Equals(OrionDeadline other)
        => ReferenceEquals(clock, other.clock) && startTimestamp == other.startTimestamp && budget == other.budget;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OrionDeadline other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
        => HashCode.Combine(clock is null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(clock), startTimestamp, budget);

    /// <summary>Equality over the deadline's budget and origin.</summary>
    public static bool operator ==(OrionDeadline left, OrionDeadline right) => left.Equals(right);

    /// <summary>Inequality over the deadline's budget and origin.</summary>
    public static bool operator !=(OrionDeadline left, OrionDeadline right) => !left.Equals(right);
}
