namespace Moongazing.Orion.Abstractions.Tests;

using Moongazing.Orion.Abstractions.Time;
using Xunit;

public sealed class SystemOrionClockTests
{
    [Fact]
    public void Default_constructor_forwards_to_the_system_time_provider()
    {
        var sut = new SystemOrionClock();

        // We cannot assert an exact instant against the wall clock, but the value should be
        // current to within a generous tolerance, proving it forwards to TimeProvider.System.
        var delta = (DateTimeOffset.UtcNow - sut.UtcNow).Duration();
        Assert.True(delta < TimeSpan.FromMinutes(1), $"UtcNow drifted by {delta}.");
    }

    [Fact]
    public void Constructor_rejects_a_null_time_provider()
    {
        Assert.Throws<ArgumentNullException>(() => new SystemOrionClock(null!));
    }

    [Fact]
    public void UtcNow_forwards_to_the_backing_provider()
    {
        var instant = new DateTimeOffset(2026, 6, 18, 9, 30, 0, TimeSpan.Zero);
        var provider = new ControllableTimeProvider(instant);
        var sut = new SystemOrionClock(provider);

        Assert.Equal(instant, sut.UtcNow);

        provider.SetUtcNow(instant.AddHours(2));
        Assert.Equal(instant.AddHours(2), sut.UtcNow);
    }

    [Fact]
    public void GetTimestamp_forwards_to_the_backing_provider()
    {
        var provider = new ControllableTimeProvider(DateTimeOffset.UnixEpoch) { Timestamp = 12_345 };
        var sut = new SystemOrionClock(provider);

        Assert.Equal(12_345, sut.GetTimestamp());
    }

    [Fact]
    public void GetElapsedTime_uses_the_provider_frequency()
    {
        var provider = new ControllableTimeProvider(DateTimeOffset.UnixEpoch) { Timestamp = 0 };
        var sut = new SystemOrionClock(provider);

        var start = sut.GetTimestamp();
        provider.Timestamp = provider.TimestampFrequency * 3; // three seconds' worth of ticks

        Assert.Equal(TimeSpan.FromSeconds(3), sut.GetElapsedTime(start));
    }

    /// <summary>
    /// A minimal, dependency-free <see cref="TimeProvider"/> whose wall clock and monotonic
    /// timestamp are fully controllable, so the forwarding behaviour of
    /// <see cref="SystemOrionClock"/> can be asserted deterministically.
    /// </summary>
    private sealed class ControllableTimeProvider : TimeProvider
    {
        private DateTimeOffset utcNow;

        public ControllableTimeProvider(DateTimeOffset start) => utcNow = start;

        public long Timestamp { get; set; }

        public override DateTimeOffset GetUtcNow() => utcNow;

        public override long GetTimestamp() => Timestamp;

        public void SetUtcNow(DateTimeOffset value) => utcNow = value;
    }
}
