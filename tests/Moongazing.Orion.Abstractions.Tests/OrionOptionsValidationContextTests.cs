namespace Moongazing.Orion.Abstractions.Tests;

using Moongazing.Orion.Abstractions.Configuration;
using Xunit;

public sealed class OrionOptionsValidationContextTests
{
    private static OrionOptionsValidationContext Context(string? name = null) =>
        new(typeof(SampleOptions), name);

    [Fact]
    public void A_fresh_context_has_no_failures()
    {
        var context = Context();

        Assert.False(context.HasFailures);
        Assert.Empty(context.Failures);
    }

    [Fact]
    public void AddFailure_prefixes_the_options_type_and_property()
    {
        var context = Context();

        context.AddFailure("LeaseDuration", "must be greater than zero.");

        Assert.True(context.HasFailures);
        Assert.Equal("SampleOptions.LeaseDuration: must be greater than zero.", Assert.Single(context.Failures));
    }

    [Fact]
    public void AddFailure_without_a_property_prefixes_only_the_options_type()
    {
        var context = Context();

        context.AddFailure("RenewalInterval must be shorter than LeaseDuration.");

        Assert.Equal(
            "SampleOptions: RenewalInterval must be shorter than LeaseDuration.",
            Assert.Single(context.Failures));
    }

    [Fact]
    public void Require_reports_only_when_the_invariant_does_not_hold()
    {
        var context = Context();

        context.Require(condition: true, "never reported.");
        Assert.False(context.HasFailures);

        context.Require(condition: false, "reported.");
        Assert.True(context.HasFailures);
    }

    [Theory]
    [InlineData(null, "must be set.")]
    [InlineData("", "must not be empty or whitespace.")]
    [InlineData("   ", "must not be empty or whitespace.")]
    public void RequireNotNullOrWhiteSpace_distinguishes_unset_from_blank(string? value, string expectedReason)
    {
        var context = Context();

        context.RequireNotNullOrWhiteSpace(value, "Prefix");

        Assert.Equal($"SampleOptions.Prefix: {expectedReason}", Assert.Single(context.Failures));
    }

    [Fact]
    public void RequireNotNullOrWhiteSpace_accepts_a_set_value()
    {
        var context = Context();

        context.RequireNotNullOrWhiteSpace("ok", "Prefix");

        Assert.False(context.HasFailures);
    }

    [Fact]
    public void RequirePositive_rejects_zero_and_negative_durations()
    {
        var context = Context();

        context.RequirePositive(TimeSpan.Zero, "Zero");
        context.RequirePositive(TimeSpan.FromSeconds(-1), "Negative");
        context.RequirePositive(TimeSpan.FromTicks(1), "Positive");

        Assert.Equal(2, context.Failures.Count);
        Assert.Contains("Zero", context.Failures[0], StringComparison.Ordinal);
        Assert.Contains("Negative", context.Failures[1], StringComparison.Ordinal);
    }

    [Fact]
    public void RequirePositive_rejects_zero_and_negative_counts()
    {
        var context = Context();

        context.RequirePositive(0, "Zero");
        context.RequirePositive(-1, "Negative");
        context.RequirePositive(1, "Positive");

        Assert.Equal(2, context.Failures.Count);
    }

    [Fact]
    public void RequireNonNegative_accepts_zero()
    {
        var context = Context();

        context.RequireNonNegative(0, "Count");
        context.RequireNonNegative(TimeSpan.Zero, "Duration");
        context.RequireNonNegative(-1, "NegativeCount");
        context.RequireNonNegative(TimeSpan.FromSeconds(-1), "NegativeDuration");

        Assert.Equal(2, context.Failures.Count);
        Assert.Contains("NegativeCount", context.Failures[0], StringComparison.Ordinal);
        Assert.Contains("NegativeDuration", context.Failures[1], StringComparison.Ordinal);
    }

    [Fact]
    public void RequireInRange_bounds_are_inclusive()
    {
        var context = Context();

        context.RequireInRange(1, 1, 3, "Low");
        context.RequireInRange(3, 1, 3, "High");
        context.RequireInRange(4, 1, 3, "Over");
        context.RequireInRange(0, 1, 3, "Under");

        Assert.Equal(2, context.Failures.Count);
        Assert.Contains("Over", context.Failures[0], StringComparison.Ordinal);
        Assert.Contains("Under", context.Failures[1], StringComparison.Ordinal);
    }

    [Fact]
    public void RequireInRange_bounds_are_inclusive_for_durations()
    {
        var context = Context();
        var min = TimeSpan.FromSeconds(1);
        var max = TimeSpan.FromSeconds(3);

        context.RequireInRange(min, min, max, "Low");
        context.RequireInRange(max, min, max, "High");
        context.RequireInRange(TimeSpan.FromSeconds(4), min, max, "Over");

        Assert.Single(context.Failures);
    }

    [Fact]
    public void Failures_are_reported_in_order()
    {
        var context = Context();

        context.AddFailure("A", "first.");
        context.AddFailure("B", "second.");
        context.AddFailure("C", "third.");

        Assert.Equal(
            ["SampleOptions.A: first.", "SampleOptions.B: second.", "SampleOptions.C: third."],
            context.Failures);
    }

    [Fact]
    public void The_named_options_name_is_surfaced_and_the_default_instance_reports_null()
    {
        Assert.Null(Context().OptionsName);
        Assert.Equal("primary", Context("primary").OptionsName);
        Assert.Equal(typeof(SampleOptions), Context().OptionsType);
    }

    [Fact]
    public void AddFailure_rejects_a_blank_property_or_reason()
    {
        var context = Context();

        Assert.Throws<ArgumentException>(() => context.AddFailure(" ", "reason."));
        Assert.Throws<ArgumentException>(() => context.AddFailure("Property", " "));
        Assert.Throws<ArgumentException>(() => context.AddFailure(" "));
    }

    private sealed class SampleOptions : OrionOptions;
}
