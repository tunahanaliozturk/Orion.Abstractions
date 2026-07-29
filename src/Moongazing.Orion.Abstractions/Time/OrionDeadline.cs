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
/// the family's acquire/retry/renew loops, giving them one testable shape. A deadline created from one
/// clock must be checked against that same clock.
/// </para>
/// </summary>
public readonly struct OrionDeadline : IEquatable<OrionDeadline>
{
    private readonly long startTimestamp;
    private readonly TimeSpan budget;
    private readonly bool hasBudget;

    private OrionDeadline(long startTimestamp, TimeSpan budget)
    {
        this.startTimestamp = startTimestamp;
        this.budget = budget;
        hasBudget = true;
    }

    /// <summary>A deadline that never expires — the "no timeout" case. <see cref="IsExpired"/> is always false.</summary>
    public static OrionDeadline Never => default;

    /// <summary>
    /// A deadline <paramref name="budget"/> from now, measured on <paramref name="clock"/>'s monotonic
    /// timestamp. A non-positive budget yields an already-expired deadline. A budget of
    /// <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> yields <see cref="Never"/>.
    /// </summary>
    /// <param name="clock">The clock the deadline is measured on (and must later be checked against).</param>
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
        return new OrionDeadline(clock.GetTimestamp(), clamped);
    }

    /// <summary>True when this deadline carries a finite budget; false for <see cref="Never"/>.</summary>
    public bool HasBudget => hasBudget;

    /// <summary>
    /// Whether the deadline has passed on <paramref name="clock"/>. Always false for <see cref="Never"/>.
    /// Pass the same clock the deadline was created from.
    /// </summary>
    /// <param name="clock">The clock to measure against (the one used to create the deadline).</param>
    /// <returns>True when the elapsed time has reached the budget.</returns>
    public bool IsExpired(IOrionClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        return hasBudget && clock.GetElapsedTime(startTimestamp) >= budget;
    }

    /// <summary>
    /// The time left before the deadline on <paramref name="clock"/>, clamped to zero once passed.
    /// Returns <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> for <see cref="Never"/>.
    /// </summary>
    /// <param name="clock">The clock to measure against (the one used to create the deadline).</param>
    /// <returns>The remaining time, never negative; infinite for <see cref="Never"/>.</returns>
    public TimeSpan Remaining(IOrionClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (!hasBudget)
        {
            return System.Threading.Timeout.InfiniteTimeSpan;
        }
        var remaining = budget - clock.GetElapsedTime(startTimestamp);
        return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }

    /// <inheritdoc />
    public bool Equals(OrionDeadline other)
        => hasBudget == other.hasBudget && startTimestamp == other.startTimestamp && budget == other.budget;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OrionDeadline other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(hasBudget, startTimestamp, budget);

    /// <summary>Equality over the deadline's budget and origin.</summary>
    public static bool operator ==(OrionDeadline left, OrionDeadline right) => left.Equals(right);

    /// <summary>Inequality over the deadline's budget and origin.</summary>
    public static bool operator !=(OrionDeadline left, OrionDeadline right) => !left.Equals(right);
}
