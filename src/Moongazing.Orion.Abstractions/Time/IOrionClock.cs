namespace Moongazing.Orion.Abstractions.Time;

/// <summary>
/// A minimal clock abstraction every Orion background worker / lease / scheduler can depend
/// on for deterministic testing. Production binds <see cref="SystemOrionClock"/> (which
/// forwards to <see cref="TimeProvider.System"/>); tests bind a frozen / advanceable clock.
/// </summary>
/// <remarks>
/// This is intentionally a thin seam over <see cref="TimeProvider"/> rather than a
/// re-implementation: it exists so Orion packages share ONE clock contract (and one DI
/// registration name) instead of each re-declaring an <c>IClock</c>.
/// </remarks>
public interface IOrionClock
{
    /// <summary>The current UTC instant.</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>
    /// A high-frequency monotonic timestamp for measuring elapsed time, in the same units as
    /// <see cref="System.Diagnostics.Stopwatch.GetTimestamp"/>. Use with
    /// <see cref="GetElapsedTime(long)"/> rather than subtracting <see cref="UtcNow"/> values
    /// (which are subject to wall-clock adjustment).
    /// </summary>
    long GetTimestamp();

    /// <summary>Elapsed time since a timestamp returned by <see cref="GetTimestamp"/>.</summary>
    /// <param name="startingTimestamp">A prior <see cref="GetTimestamp"/> value.</param>
    TimeSpan GetElapsedTime(long startingTimestamp);
}
