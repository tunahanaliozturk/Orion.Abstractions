namespace Moongazing.Orion.Abstractions.Demo;

using Moongazing.Orion.Abstractions.Time;

/// <summary>
/// Demonstrates <see cref="IOrionClock"/> / <see cref="SystemOrionClock"/>: the production
/// clock over <see cref="TimeProvider.System"/> for real wall-clock and monotonic elapsed
/// timing, plus a clock over a custom <see cref="TimeProvider"/> to show how the same seam
/// makes elapsed-time logic deterministic (the production analogue of the test-only
/// FrozenOrionClock shipped in Orion.Abstractions.Testing).
/// </summary>
internal sealed class ClockDemo
{
    public async Task RunAsync()
    {
        ConsoleUi.Step("1) Production clock over TimeProvider.System:");
        IOrionClock clock = new SystemOrionClock();
        ConsoleUi.Step($"   UtcNow = {clock.UtcNow:O}");

        var start = clock.GetTimestamp();
        await Task.Delay(25);
        var elapsed = clock.GetElapsedTime(start);
        ConsoleUi.Step($"   Measured elapsed over a ~25ms delay: {elapsed.TotalMilliseconds:F1} ms");
        ConsoleUi.Step("   (monotonic timestamp, immune to wall-clock adjustment)");

        ConsoleUi.Step("2) Same seam over a controllable TimeProvider => deterministic time:");
        var fake = new ManualTimeProvider(new DateTimeOffset(2026, 6, 18, 12, 0, 0, TimeSpan.Zero));
        IOrionClock controllable = new SystemOrionClock(fake);
        ConsoleUi.Step($"   UtcNow = {controllable.UtcNow:O}");

        var leaseStart = controllable.GetTimestamp();
        fake.Advance(TimeSpan.FromSeconds(31));
        var leaseElapsed = controllable.GetElapsedTime(leaseStart);
        ConsoleUi.Step($"   After advancing 31s with no real delay, elapsed = {leaseElapsed.TotalSeconds:F0}s");
        ConsoleUi.Step($"   UtcNow advanced to {controllable.UtcNow:O}");
        ConsoleUi.Step("   -> lease-expiry / grace-period logic becomes testable and instant");
    }

    /// <summary>
    /// A minimal advanceable <see cref="TimeProvider"/> for the demo. In real tests you would
    /// inject FrozenOrionClock from Orion.Abstractions.Testing instead of hand-rolling this.
    /// </summary>
    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset utcNow;
        private long timestamp;

        public ManualTimeProvider(DateTimeOffset start)
        {
            utcNow = start;
            timestamp = 0;
        }

        public override DateTimeOffset GetUtcNow() => utcNow;

        public override long GetTimestamp() => timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public void Advance(TimeSpan delta)
        {
            utcNow = utcNow.Add(delta);
            timestamp += (long)(delta.TotalSeconds * TimestampFrequency);
        }
    }
}
