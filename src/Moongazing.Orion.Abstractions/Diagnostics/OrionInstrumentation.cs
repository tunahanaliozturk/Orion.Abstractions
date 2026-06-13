namespace Moongazing.Orion.Abstractions.Diagnostics;

using System.Diagnostics;
using System.Diagnostics.Metrics;

/// <summary>
/// Base class that codifies the Orion family's OpenTelemetry conventions so every member
/// instruments the same way:
/// <list type="bullet">
/// <item>An <see cref="ActivitySource"/> and a <see cref="Meter"/> sharing one name.</item>
/// <item>A set of static tags (set once at startup via <c>WithMetricsLabel</c>) appended
/// to every measurement, for multi-tenant / multi-region dashboard splitting without a
/// separate Meter.</item>
/// </list>
/// Members derive a sealed diagnostics class from this, expose their instruments, and
/// stamp measurements through <see cref="StaticTags"/> / <see cref="Tag(KeyValuePair{string, object?})"/>.
/// </summary>
public abstract class OrionInstrumentation : IDisposable
{
    private volatile KeyValuePair<string, object?>[] staticTags = Array.Empty<KeyValuePair<string, object?>>();

    /// <summary>
    /// Create an instrumentation surface named <paramref name="name"/> (used for both the
    /// ActivitySource and the Meter) versioned with <paramref name="version"/>.
    /// </summary>
    /// <param name="name">The instrumentation name, e.g. <c>Moongazing.OrionLock</c>.</param>
    /// <param name="version">The package version string, e.g. <c>0.1.0</c>.</param>
    protected OrionInstrumentation(string name, string version)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(version);
        ActivitySource = new ActivitySource(name, version);
        Meter = new Meter(name, version);
    }

    /// <summary>The activity source members write spans into.</summary>
    public ActivitySource ActivitySource { get; }

    /// <summary>The meter members create counters / histograms / gauges on.</summary>
    public Meter Meter { get; }

    /// <summary>
    /// The static tags appended to every measurement. Exposed as a snapshot so an emission
    /// site never observes a half-built array while <see cref="SetStaticTags"/> runs.
    /// </summary>
    public KeyValuePair<string, object?>[] StaticTags => staticTags;

    /// <summary>
    /// Replace the static tag set. Intended to be called ONCE at startup (single-threaded);
    /// tags configured after the host starts emitting do NOT retroactively apply to
    /// already-emitted measurements.
    /// </summary>
    /// <param name="tags">The tag key/value pairs, e.g. tenant / region labels.</param>
    public void SetStaticTags(IReadOnlyDictionary<string, string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        var snapshot = new KeyValuePair<string, object?>[tags.Count];
        var i = 0;
        foreach (var (k, v) in tags)
        {
            snapshot[i++] = new KeyValuePair<string, object?>(k, v);
        }
        staticTags = snapshot;
    }

    /// <summary>
    /// Combine the static tags with one additional tag, returning an array suitable for a
    /// tagged measurement. When no static tags are configured the result is a single-element
    /// array, so the common (untagged) path stays allocation-light.
    /// </summary>
    /// <param name="extra">The per-measurement tag to append.</param>
    public KeyValuePair<string, object?>[] Tag(KeyValuePair<string, object?> extra)
    {
        var s = staticTags;
        if (s.Length == 0)
        {
            return new[] { extra };
        }
        var combined = new KeyValuePair<string, object?>[s.Length + 1];
        Array.Copy(s, combined, s.Length);
        combined[s.Length] = extra;
        return combined;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Disposes the ActivitySource and Meter. Override to dispose extra resources.</summary>
    /// <param name="disposing">True when called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            ActivitySource.Dispose();
            Meter.Dispose();
        }
    }
}
