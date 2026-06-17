using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Moongazing.Orion.Abstractions.Time;

namespace Moongazing.Orion.Abstractions.Benchmarks;

/// <summary>
/// Measures the <see cref="SystemOrionClock"/> forwarding overhead over
/// <see cref="TimeProvider"/>. Leases, schedulers, and background workers across the family
/// read the clock frequently, so the cost of the thin seam (versus calling the provider
/// directly) is worth knowing. Invoked through the <see cref="IOrionClock"/> interface to
/// include the virtual dispatch every consumer pays.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net90)]
public class OrionClockBenchmarks
{
    private readonly IOrionClock clock = new SystemOrionClock();
    private long timestamp;

    [GlobalSetup]
    public void Setup() => timestamp = clock.GetTimestamp();

    /// <summary>Read the current UTC instant through the clock seam.</summary>
    [Benchmark]
    public DateTimeOffset UtcNow() => clock.UtcNow;

    /// <summary>Read a monotonic timestamp through the clock seam.</summary>
    [Benchmark]
    public long GetTimestamp() => clock.GetTimestamp();

    /// <summary>Compute elapsed time from a prior timestamp through the clock seam.</summary>
    [Benchmark]
    public TimeSpan GetElapsedTime() => clock.GetElapsedTime(timestamp);
}
