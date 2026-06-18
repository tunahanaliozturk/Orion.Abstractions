namespace Moongazing.Orion.Abstractions.Tests;

using Moongazing.Orion.Abstractions.Testing;
using Xunit;

public sealed class FrozenOrionClockTests
{
    [Fact]
    public void Starts_frozen_and_advances_both_clocks()
    {
        var clock = new FrozenOrionClock(new DateTimeOffset(2026, 6, 14, 0, 0, 0, TimeSpan.Zero));
        var start = clock.GetTimestamp();

        Assert.Equal(new DateTimeOffset(2026, 6, 14, 0, 0, 0, TimeSpan.Zero), clock.UtcNow);
        Assert.Equal(TimeSpan.Zero, clock.GetElapsedTime(start));

        clock.Advance(TimeSpan.FromSeconds(90));

        Assert.Equal(new DateTimeOffset(2026, 6, 14, 0, 1, 30, TimeSpan.Zero), clock.UtcNow);
        Assert.Equal(TimeSpan.FromSeconds(90), clock.GetElapsedTime(start));
    }

    [Fact]
    public void Advance_rejects_negative_delta()
    {
        var clock = new FrozenOrionClock();
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void SetUtcNow_rejects_going_backward()
    {
        var clock = new FrozenOrionClock(new DateTimeOffset(2026, 6, 14, 12, 0, 0, TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            clock.SetUtcNow(new DateTimeOffset(2026, 6, 14, 11, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void Default_start_is_the_documented_2026_epoch()
    {
        var clock = new FrozenOrionClock();
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), clock.UtcNow);
    }

    [Fact]
    public void Advance_accepts_a_zero_delta_as_a_noop()
    {
        var clock = new FrozenOrionClock(new DateTimeOffset(2026, 6, 14, 0, 0, 0, TimeSpan.Zero));
        var start = clock.GetTimestamp();

        clock.Advance(TimeSpan.Zero);

        Assert.Equal(new DateTimeOffset(2026, 6, 14, 0, 0, 0, TimeSpan.Zero), clock.UtcNow);
        Assert.Equal(TimeSpan.Zero, clock.GetElapsedTime(start));
    }

    [Fact]
    public void Advance_is_cumulative()
    {
        var clock = new FrozenOrionClock(new DateTimeOffset(2026, 6, 14, 0, 0, 0, TimeSpan.Zero));
        var start = clock.GetTimestamp();

        clock.Advance(TimeSpan.FromSeconds(30));
        clock.Advance(TimeSpan.FromSeconds(15));

        Assert.Equal(new DateTimeOffset(2026, 6, 14, 0, 0, 45, TimeSpan.Zero), clock.UtcNow);
        Assert.Equal(TimeSpan.FromSeconds(45), clock.GetElapsedTime(start));
    }

    [Fact]
    public void SetUtcNow_moves_the_wall_clock_without_moving_the_monotonic_timestamp()
    {
        var clock = new FrozenOrionClock(new DateTimeOffset(2026, 6, 14, 0, 0, 0, TimeSpan.Zero));
        var start = clock.GetTimestamp();

        clock.SetUtcNow(new DateTimeOffset(2026, 6, 14, 6, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateTimeOffset(2026, 6, 14, 6, 0, 0, TimeSpan.Zero), clock.UtcNow);
        // The monotonic timestamp is deliberately independent of wall-clock jumps.
        Assert.Equal(TimeSpan.Zero, clock.GetElapsedTime(start));
    }

    [Fact]
    public void SetUtcNow_allows_setting_to_the_current_instant()
    {
        var now = new DateTimeOffset(2026, 6, 14, 12, 0, 0, TimeSpan.Zero);
        var clock = new FrozenOrionClock(now);

        // Equal (not earlier) is permitted; only strictly-earlier is rejected.
        clock.SetUtcNow(now);
        Assert.Equal(now, clock.UtcNow);
    }
}
