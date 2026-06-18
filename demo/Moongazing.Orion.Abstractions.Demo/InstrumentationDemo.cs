namespace Moongazing.Orion.Abstractions.Demo;

using System.Diagnostics.Metrics;
using Moongazing.Orion.Abstractions.Diagnostics;

/// <summary>
/// Demonstrates <see cref="OrionInstrumentation"/>: a derived diagnostics class pairs one
/// ActivitySource and one Meter under a shared name, and <c>SetStaticTags</c> + <c>Tag</c>
/// stamp every measurement so dashboards split by tenant / region without a second Meter.
/// A real <see cref="MeterListener"/> is attached to print the tags actually observed.
/// </summary>
internal sealed class InstrumentationDemo
{
    /// <summary>A sealed diagnostics surface, exactly as an Orion library would declare one.</summary>
    private sealed class CheckoutDiagnostics : OrionInstrumentation
    {
        public CheckoutDiagnostics()
            : base("Moongazing.Orion.Demo.Checkout", "0.1.0")
        {
            OrdersProcessed = Meter.CreateCounter<long>("orders.processed");
        }

        public Counter<long> OrdersProcessed { get; }
    }

    public void Run()
    {
        using var diag = new CheckoutDiagnostics();

        ConsoleUi.Step($"ActivitySource name : {diag.ActivitySource.Name}");
        ConsoleUi.Step($"Meter name          : {diag.Meter.Name} (shared name + version)");

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == diag.Meter.Name)
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            var rendered = string.Join(", ", FormatTags(tags));
            ConsoleUi.Step($"   MeterListener saw {instrument.Name} += {measurement} with tags [{rendered}]");
        });
        listener.Start();

        ConsoleUi.Step("1) Before SetStaticTags, Tag(...) stays allocation-light (one tag only):");
        diag.OrdersProcessed.Add(1, diag.Tag(new("outcome", "ok")));

        ConsoleUi.Step("2) Set static tags once at startup (tenant + region):");
        diag.SetStaticTags(new Dictionary<string, string>
        {
            ["tenant"] = "acme",
            ["region"] = "eu-west",
        });
        ConsoleUi.Step($"   StaticTags now: [{string.Join(", ", diag.StaticTags.Select(t => $"{t.Key}={t.Value}"))}]");

        ConsoleUi.Step("3) Subsequent measurements are stamped with the static tags + the per-call tag:");
        diag.OrdersProcessed.Add(1, diag.Tag(new("outcome", "ok")));
        diag.OrdersProcessed.Add(1, diag.Tag(new("outcome", "declined")));

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
