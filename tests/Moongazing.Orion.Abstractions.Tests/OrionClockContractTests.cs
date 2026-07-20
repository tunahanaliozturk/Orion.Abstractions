namespace Moongazing.Orion.Abstractions.Tests;

using System.Globalization;

using Moongazing.Orion.Abstractions.Testing;
using Moongazing.Orion.Abstractions.Time;
using Xunit;

/// <summary>
/// The frozen <see cref="IOrionClock"/> surface, asserted against every implementation the
/// family ships so the two seams can never disagree.
/// </summary>
public sealed class OrionClockContractTests
{
    public static TheoryData<IOrionClock> Clocks =>
    [
        new SystemOrionClock(),
        new FrozenOrionClock(DateTimeOffset.Parse("2026-07-01T00:00:00Z", CultureInfo.InvariantCulture)),
    ];

    [Theory]
    [MemberData(nameof(Clocks))]
    public void GetUtcNow_agrees_with_UtcNow(IOrionClock clock)
    {
        // The default interface implementation must forward, not re-read: on a moving clock
        // the two reads are taken back-to-back, so they may differ only by the elapsed instant.
        var viaProperty = clock.UtcNow;
        var viaMethod = clock.GetUtcNow();

        Assert.True(
            viaMethod - viaProperty < TimeSpan.FromSeconds(1),
            $"GetUtcNow() ({viaMethod:O}) diverged from UtcNow ({viaProperty:O}).");
        Assert.True(viaMethod >= viaProperty);
    }

    [Fact]
    public void GetUtcNow_is_exactly_UtcNow_on_a_stopped_clock()
    {
        var instant = DateTimeOffset.Parse("2026-07-01T12:34:56Z", CultureInfo.InvariantCulture);
        IOrionClock clock = new FrozenOrionClock(instant);

        Assert.Equal(instant, clock.GetUtcNow());
        Assert.Equal(clock.UtcNow, clock.GetUtcNow());
    }

    [Theory]
    [MemberData(nameof(Clocks))]
    public void GetElapsedTime_is_never_negative_for_a_prior_timestamp(IOrionClock clock)
    {
        var start = clock.GetTimestamp();

        Assert.True(clock.GetElapsedTime(start) >= TimeSpan.Zero);
    }
}
