namespace Moongazing.Orion.Abstractions.Tests;

using Moongazing.Orion.Abstractions.Diagnostics;
using Xunit;

public sealed class OrionTelemetryTests
{
    [Fact]
    public void ScopeName_prefixes_a_bare_package_name()
    {
        Assert.Equal("Moongazing.OrionLock", OrionTelemetry.ScopeName("OrionLock"));
    }

    [Fact]
    public void ScopeName_is_idempotent_for_an_already_qualified_name()
    {
        Assert.Equal("Moongazing.OrionLock", OrionTelemetry.ScopeName("Moongazing.OrionLock"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ScopeName_rejects_a_blank_package_name(string? packageName)
    {
        Assert.ThrowsAny<ArgumentException>(() => OrionTelemetry.ScopeName(packageName!));
    }

    [Fact]
    public void MetricName_joins_the_component_and_instrument_under_the_orion_prefix()
    {
        Assert.Equal("orion.lock.acquire.duration", OrionTelemetry.MetricName("lock", "acquire.duration"));
    }

    [Theory]
    [InlineData(null, "acquire")]
    [InlineData("", "acquire")]
    [InlineData("lock", null)]
    [InlineData("lock", " ")]
    public void MetricName_rejects_blank_parts(string? component, string? instrument)
    {
        Assert.ThrowsAny<ArgumentException>(() => OrionTelemetry.MetricName(component!, instrument!));
    }

    [Fact]
    public void The_instrumentation_instance_tag_key_is_the_frozen_naming_surface_value()
    {
        // These two must never drift: OrionInstrumentation predates the naming surface and
        // consumers depend on the literal value.
        Assert.Equal("orion.instance", OrionTelemetry.Tags.Instance);
        Assert.Equal(OrionTelemetry.Tags.Instance, OrionInstrumentation.InstanceTagKey);
    }

    [Fact]
    public void Every_frozen_tag_key_carries_the_orion_prefix()
    {
        string[] keys =
        [
            OrionTelemetry.Tags.Instance,
            OrionTelemetry.Tags.Package,
            OrionTelemetry.Tags.Tenant,
            OrionTelemetry.Tags.Region,
            OrionTelemetry.Tags.Operation,
            OrionTelemetry.Tags.Outcome,
            OrionTelemetry.Tags.ErrorCode,
            OrionTelemetry.Tags.Attempt,
        ];

        Assert.All(keys, key => Assert.StartsWith(OrionTelemetry.MetricPrefix, key, StringComparison.Ordinal));
        Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void The_outcome_vocabulary_is_the_frozen_low_cardinality_set()
    {
        Assert.Equal("success", OrionTelemetry.Outcomes.Success);
        Assert.Equal("failure", OrionTelemetry.Outcomes.Failure);
        Assert.Equal("cancelled", OrionTelemetry.Outcomes.Cancelled);
        Assert.Equal("timeout", OrionTelemetry.Outcomes.Timeout);
    }
}
