namespace Moongazing.Orion.Abstractions.Tests;

using System.Diagnostics.Metrics;

using Moongazing.Orion.Abstractions.Diagnostics;

using Xunit;

/// <summary>
/// <see cref="MeterListener"/> subscribes to instruments process-wide by name, so these tests
/// must not run alongside any other test that creates a same-named Meter or they would observe
/// each other's measurements. They live in their own collection with parallelization disabled.
/// Every listener here also filters to a single instrument/meter instance via
/// <see cref="OrionInstrumentation.ListensTo(System.Diagnostics.Metrics.Instrument, OrionInstrumentation)"/>,
/// which is the very behavior under test.
/// </summary>
[Collection(nameof(MeterListenerTestGroup))]
public sealed class OrionInstrumentationMeterListenerTests
{
    private const string SharedMeterName = "Moongazing.OrionListenerTest";

    private sealed class ScopedInstrumentation : OrionInstrumentation
    {
        public ScopedInstrumentation(string instanceScopeId)
            : base(SharedMeterName, "9.9.9", instanceScopeId)
        {
        }
    }

    [Fact]
    public void A_listener_filtered_to_one_instance_does_not_double_count_a_sibling()
    {
        // Two diagnostics instances share the SAME Meter name (the whole point of the bug):
        // a plain name-filtered listener would attribute both instances' measurements together.
        using var first = new ScopedInstrumentation("inst-1");
        using var second = new ScopedInstrumentation("inst-2");

        var firstCounter = first.Meter.CreateCounter<long>("operations");
        var secondCounter = second.Meter.CreateCounter<long>("operations");

        long observedForFirst = 0;

        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                // Filter to exactly the first instance's instruments by reference identity.
                if (OrionInstrumentation.ListensTo(instrument, first))
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) => observedForFirst += measurement);
        listener.Start();

        firstCounter.Add(5);
        secondCounter.Add(100); // Belongs to the sibling instance; must NOT be counted.
        firstCounter.Add(2);

        Assert.Equal(7, observedForFirst);
    }

    [Fact]
    public void Two_instances_are_independently_observable_by_separate_listeners()
    {
        using var first = new ScopedInstrumentation("inst-1");
        using var second = new ScopedInstrumentation("inst-2");

        var firstCounter = first.Meter.CreateCounter<long>("operations");
        var secondCounter = second.Meter.CreateCounter<long>("operations");

        long observedForFirst = 0;
        long observedForSecond = 0;

        using var firstListener = BuildListenerFor(first, m => observedForFirst += m);
        using var secondListener = BuildListenerFor(second, m => observedForSecond += m);

        firstCounter.Add(10);
        secondCounter.Add(3);
        secondCounter.Add(4);

        Assert.Equal(10, observedForFirst);
        Assert.Equal(7, observedForSecond);
    }

    [Fact]
    public void The_scope_tag_is_visible_to_a_listener_via_the_instrument_meter()
    {
        using var instance = new ScopedInstrumentation("inst-42");
        var counter = instance.Meter.CreateCounter<long>("operations");

        string? observedScopeId = null;

        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (!OrionInstrumentation.ListensTo(instrument, instance))
                {
                    return;
                }

                foreach (var tag in instrument.Meter.Tags ?? [])
                {
                    if (tag.Key == OrionInstrumentation.InstanceTagKey)
                    {
                        observedScopeId = (string?)tag.Value;
                    }
                }

                l.EnableMeasurementEvents(instrument);
            },
        };
        listener.Start();

        counter.Add(1);

        Assert.Equal("inst-42", observedScopeId);
    }

    private static MeterListener BuildListenerFor(OrionInstrumentation instance, Action<long> onMeasurement)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (OrionInstrumentation.ListensTo(instrument, instance))
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) => onMeasurement(measurement));
        listener.Start();
        return listener;
    }
}

/// <summary>
/// Test group that disables xunit parallelization for the <see cref="MeterListener"/> tests,
/// which observe process-wide instrument publication and would otherwise race.
/// </summary>
[CollectionDefinition(nameof(MeterListenerTestGroup), DisableParallelization = true)]
public sealed class MeterListenerTestGroup
{
}
