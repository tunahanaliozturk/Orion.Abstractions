namespace Moongazing.Orion.Abstractions.Demo;

using System.Diagnostics.Metrics;
using Moongazing.Orion.Abstractions.Diagnostics;

/// <summary>
/// Demonstrates instance-scoped instrumentation: two diagnostics instances share the same
/// Meter name but each is given a distinct instance scope id, so the Meter carries an
/// <c>orion.instance</c> tag (plus optional custom tags) for per-instance partitioning.
/// <see cref="OrionInstrumentation.ListensTo"/> then filters a <see cref="MeterListener"/>
/// to exactly one instance's instruments, even though both instances share the Meter name.
/// </summary>
internal sealed class InstanceScopedInstrumentationDemo
{
    /// <summary>A sealed diagnostics surface that opts into a per-instance scope.</summary>
    private sealed class WorkerDiagnostics : OrionInstrumentation
    {
        public WorkerDiagnostics(string instanceScopeId, IReadOnlyDictionary<string, string>? instanceTags = null)
            : base("Moongazing.Orion.Demo.Worker", "0.1.0", instanceScopeId, instanceTags)
        {
            JobsProcessed = Meter.CreateCounter<long>("jobs.processed");
        }

        public Counter<long> JobsProcessed { get; }
    }

    public void Run()
    {
        // Two instances, same Meter name, distinct instance scope ids. The second also carries
        // an extra Meter-level tag merged alongside the reserved orion.instance tag.
        using var first = new WorkerDiagnostics("worker-1");
        using var second = new WorkerDiagnostics(
            "worker-2",
            new Dictionary<string, string> { ["partition"] = "p7" });

        ConsoleUi.Step($"Both Meters share the name : {first.Meter.Name}");
        ConsoleUi.Step($"first.InstanceScopeId      : {first.InstanceScopeId}");
        ConsoleUi.Step($"second.InstanceScopeId     : {second.InstanceScopeId}");
        ConsoleUi.Step($"Instance tag key           : {OrionInstrumentation.InstanceTagKey}");

        // Listen to the first instance only. ListensTo matches by Meter reference identity,
        // so the second instance's measurements are never enabled on this listener.
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (OrionInstrumentation.ListensTo(instrument, first))
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            var rendered = string.Join(", ", FormatTags(tags));
            ConsoleUi.Step($"   Listener (first only) saw {instrument.Name} += {measurement} with tags [{rendered}]");
        });
        listener.Start();

        ConsoleUi.Step("1) first emits (the listener observes it):");
        first.JobsProcessed.Add(1, first.Tag(new("outcome", "ok")));

        ConsoleUi.Step("2) second emits (filtered out by ListensTo, the listener stays silent):");
        second.JobsProcessed.Add(1, second.Tag(new("outcome", "ok")));

        ConsoleUi.Step("   The orion.instance tag lives on each Meter, so a collector can split per instance.");

        listener.Dispose();
    }

    private static IEnumerable<string> FormatTags(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var list = new List<string>(tags.Length);
        foreach (var tag in tags)
        {
            list.Add($"{tag.Key}={tag.Value}");
        }

        return list;
    }
}
