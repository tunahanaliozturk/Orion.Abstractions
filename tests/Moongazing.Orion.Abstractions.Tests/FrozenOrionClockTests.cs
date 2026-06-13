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
}
