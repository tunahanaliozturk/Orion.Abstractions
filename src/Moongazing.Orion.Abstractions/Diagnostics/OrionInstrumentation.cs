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
/// <para>
/// Because every diagnostics class names its <see cref="Meter"/> by a shared constant
/// (e.g. <c>Moongazing.OrionGuard</c>), a name-filtered <see cref="MeterListener"/> cannot
/// tell two live instances apart - it double-counts when a process holds several instances
/// or when tests run in parallel. To disambiguate, a derived instance can opt into an
/// <em>instance scope</em> (see <see cref="OrionInstrumentation(string, string, string?, IReadOnlyDictionary{string, string}?, object?)"/>):
/// the <see cref="Meter"/> is then created with a stable per-instance tag
/// (<see cref="InstanceTagKey"/>, default <c>orion.instance</c>) and an opaque
/// <see cref="Meter.Scope"/> object. A listener can filter precisely with
/// <see cref="ListensTo(Instrument, OrionInstrumentation)"/>.
/// </para>
/// </summary>
public abstract class OrionInstrumentation : IDisposable
{
    /// <summary>
    /// The tag key carrying the per-instance scope id on a scoped <see cref="Meter"/>.
    /// Stable across the Orion family so dashboards can split on a single well-known key.
    /// </summary>
    public const string InstanceTagKey = "orion.instance";

    private volatile KeyValuePair<string, object?>[] staticTags = Array.Empty<KeyValuePair<string, object?>>();

    /// <summary>
    /// Create an instrumentation surface named <paramref name="name"/> (used for both the
    /// ActivitySource and the Meter) versioned with <paramref name="version"/>. The Meter
    /// is created with no instance scope, so a name-filtered listener sees it exactly as a
    /// 0.1.0 consumer would.
    /// </summary>
    /// <param name="name">The instrumentation name, e.g. <c>Moongazing.OrionLock</c>.</param>
    /// <param name="version">The package version string, e.g. <c>0.1.0</c>.</param>
    protected OrionInstrumentation(string name, string version)
        : this(name, version, instanceScopeId: null, instanceTags: null, scope: null)
    {
    }

    /// <summary>
    /// Create an instrumentation surface named <paramref name="name"/> versioned with
    /// <paramref name="version"/>, attaching an <em>instance scope</em> so a
    /// <see cref="MeterListener"/> can distinguish this instance from other live instances
    /// that share the same Meter name.
    /// <para>
    /// When <paramref name="instanceScopeId"/> is non-null it is published on the
    /// <see cref="Meter"/> as the <see cref="InstanceTagKey"/> tag and surfaced as
    /// <see cref="InstanceScopeId"/>. When <paramref name="scope"/> is null an opaque scope
    /// object is synthesized so <see cref="Meter.Scope"/>
    /// is always set for a scoped instance; pass your own (e.g. the DI container or the owning
    /// instance) to link several meters under one scope. Passing all-null arguments is
    /// equivalent to the unscoped <see cref="OrionInstrumentation(string, string)"/> constructor.
    /// </para>
    /// </summary>
    /// <param name="name">The instrumentation name, e.g. <c>Moongazing.OrionLock</c>.</param>
    /// <param name="version">The package version string, e.g. <c>0.1.0</c>.</param>
    /// <param name="instanceScopeId">
    /// A stable per-instance identifier, e.g. a GUID or a host/partition id. Published on the
    /// Meter under <see cref="InstanceTagKey"/>. Null leaves the Meter untagged (the default,
    /// non-breaking behavior).
    /// </param>
    /// <param name="instanceTags">
    /// Optional additional Meter-level tags. Merged with the <see cref="InstanceTagKey"/> tag
    /// when <paramref name="instanceScopeId"/> is supplied. These stamp the Meter itself (so
    /// aggregators can split on them) and are independent of the per-measurement
    /// <see cref="StaticTags"/>.
    /// </param>
    /// <param name="scope">
    /// An optional opaque scope object linked to the Meter. When null and
    /// <paramref name="instanceScopeId"/> is non-null, a private sentinel is used so the
    /// instance is still scope-distinguishable.
    /// </param>
    protected OrionInstrumentation(
        string name,
        string version,
        string? instanceScopeId,
        IReadOnlyDictionary<string, string>? instanceTags = null,
        object? scope = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(version);

        ActivitySource = new ActivitySource(name, version);

        var scoped = instanceScopeId is not null || instanceTags is { Count: > 0 } || scope is not null;
        if (!scoped)
        {
            // Preserve the exact 0.1.0 behavior: a plain name/version Meter, no scope, no tags.
            Meter = new Meter(name, version);
            return;
        }

        InstanceScopeId = instanceScopeId;

        var options = new MeterOptions(name)
        {
            Version = version,
            // A scoped instance always carries a non-null Scope so ReferenceEquals-based
            // filtering and the Meter.Scope contract both hold, even if the caller passed none.
            Scope = scope ?? new object(),
            Tags = BuildMeterTags(instanceScopeId, instanceTags),
        };

        Meter = new Meter(options);
    }

    /// <summary>The activity source members write spans into.</summary>
    public ActivitySource ActivitySource { get; }

    /// <summary>The meter members create counters / histograms / gauges on.</summary>
    public Meter Meter { get; }

    /// <summary>
    /// The per-instance scope id published on the <see cref="Meter"/> under
    /// <see cref="InstanceTagKey"/>, or null for an unscoped (default) instance.
    /// </summary>
    public string? InstanceScopeId { get; }

    /// <summary>
    /// The static tags appended to every measurement. Exposed as a snapshot so an emission
    /// site never observes a half-built array while <see cref="SetStaticTags"/> runs.
    /// </summary>
    public KeyValuePair<string, object?>[] StaticTags => staticTags;

    /// <summary>
    /// Predicate that matches an instrument published to a <see cref="MeterListener"/> to a
    /// specific <paramref name="instance"/> by reference identity of the owning
    /// <see cref="Meter"/>. Use this inside
    /// <see cref="MeterListener.InstrumentPublished"/> to subscribe to exactly one instance's
    /// instruments - even when several instances share the same Meter name:
    /// <code>
    /// listener.InstrumentPublished = (instrument, l) =>
    /// {
    ///     if (OrionInstrumentation.ListensTo(instrument, instance))
    ///     {
    ///         l.EnableMeasurementEvents(instrument);
    ///     }
    /// };
    /// </code>
    /// This is reference-based and therefore robust even if two instances are configured with
    /// the same <see cref="InstanceScopeId"/>.
    /// </summary>
    /// <param name="instrument">The instrument offered by the listener.</param>
    /// <param name="instance">The instrumentation instance to match against.</param>
    /// <returns><see langword="true"/> when the instrument was created by the instance's Meter.</returns>
    public static bool ListensTo(Instrument instrument, OrionInstrumentation instance)
    {
        ArgumentNullException.ThrowIfNull(instrument);
        ArgumentNullException.ThrowIfNull(instance);
        return ReferenceEquals(instrument.Meter, instance.Meter);
    }

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

    private static KeyValuePair<string, object?>[] BuildMeterTags(
        string? instanceScopeId,
        IReadOnlyDictionary<string, string>? instanceTags)
    {
        if (instanceTags is not null && instanceTags.ContainsKey(InstanceTagKey))
        {
            throw new ArgumentException(
                $"'{InstanceTagKey}' is reserved for the instance scope id and cannot be supplied in instanceTags.",
                nameof(instanceTags));
        }

        var extra = instanceTags?.Count ?? 0;
        var hasInstanceTag = instanceScopeId is not null;
        var tags = new KeyValuePair<string, object?>[(hasInstanceTag ? 1 : 0) + extra];

        var i = 0;
        if (hasInstanceTag)
        {
            tags[i++] = new KeyValuePair<string, object?>(InstanceTagKey, instanceScopeId);
        }
        if (instanceTags is not null)
        {
            foreach (var (k, v) in instanceTags)
            {
                tags[i++] = new KeyValuePair<string, object?>(k, v);
            }
        }
        return tags;
    }
}
