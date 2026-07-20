namespace Moongazing.Orion.Abstractions.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moongazing.Orion.Abstractions.Configuration;
using Xunit;

public sealed class OrionOptionsValidatorTests
{
    [Fact]
    public void Options_with_no_invariants_validate_successfully()
    {
        var result = new OrionOptionsValidator<BareOptions>().Validate(name: null, new BareOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void A_single_failure_is_rendered_on_one_line()
    {
        var options = new LeaseOptions { LeaseDuration = TimeSpan.Zero };

        var result = new OrionOptionsValidator<LeaseOptions>().Validate(name: null, options);

        Assert.True(result.Failed);
        Assert.Equal(
            "LeaseOptions is invalid. LeaseOptions.LeaseDuration: must be greater than zero (was 00:00:00).",
            result.FailureMessage);
    }

    [Fact]
    public void Every_invalid_setting_is_reported_not_just_the_first()
    {
        var options = new LeaseOptions { LeaseDuration = TimeSpan.Zero, Name = "  " };

        var result = new OrionOptionsValidator<LeaseOptions>().Validate(name: null, options);

        Assert.True(result.Failed);
        Assert.Contains("LeaseOptions.LeaseDuration", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("LeaseOptions.Name", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void A_named_instance_is_identified_in_the_failure_message()
    {
        var options = new LeaseOptions { LeaseDuration = TimeSpan.Zero };

        var result = new OrionOptionsValidator<LeaseOptions>().Validate("primary", options);

        Assert.Contains("named 'primary'", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void The_default_instance_is_not_reported_as_a_blank_name()
    {
        var options = new LeaseOptions { LeaseDuration = TimeSpan.Zero };

        // Options.DefaultName is the empty string, not null - the validator must treat both
        // as "the default instance".
        var result = new OrionOptionsValidator<LeaseOptions>().Validate(Options.DefaultName, options);

        Assert.DoesNotContain("named", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void AddOrionOptions_applies_the_configuration_delegate()
    {
        var services = new ServiceCollection();
        services.AddOrionOptions<LeaseOptions>(o => o.LeaseDuration = TimeSpan.FromSeconds(30));

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<LeaseOptions>>().Value;

        Assert.Equal(TimeSpan.FromSeconds(30), options.LeaseDuration);
    }

    [Fact]
    public void AddOrionOptions_fails_resolution_when_the_configuration_is_invalid()
    {
        var services = new ServiceCollection();
        services.AddOrionOptions<LeaseOptions>(o => o.LeaseDuration = TimeSpan.Zero);

        using var provider = services.BuildServiceProvider();

        var ex = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<LeaseOptions>>().Value);
        Assert.Contains("LeaseOptions.LeaseDuration", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddOrionOptions_registers_the_validator_once_however_often_it_is_called()
    {
        var services = new ServiceCollection();
        services.AddOrionOptions<LeaseOptions>(o => o.LeaseDuration = TimeSpan.FromSeconds(1));
        services.AddOrionOptions<LeaseOptions>(o => o.Name = "renamed");

        using var provider = services.BuildServiceProvider();

        // One validator, but both configuration delegates still apply - the AddOrionX() call
        // being idempotent must not make it lossy.
        Assert.Single(provider.GetServices<IValidateOptions<LeaseOptions>>());
        var options = provider.GetRequiredService<IOptions<LeaseOptions>>().Value;
        Assert.Equal(TimeSpan.FromSeconds(1), options.LeaseDuration);
        Assert.Equal("renamed", options.Name);
    }

    [Fact]
    public void AddOrionOptions_validates_a_named_instance_independently()
    {
        var services = new ServiceCollection();
        services.AddOrionOptions<LeaseOptions>(o => o.LeaseDuration = TimeSpan.FromSeconds(5), name: "good");
        services.AddOrionOptions<LeaseOptions>(o => o.LeaseDuration = TimeSpan.Zero, name: "bad");

        using var provider = services.BuildServiceProvider();
        var monitor = provider.GetRequiredService<IOptionsMonitor<LeaseOptions>>();

        Assert.Equal(TimeSpan.FromSeconds(5), monitor.Get("good").LeaseDuration);
        Assert.Throws<OptionsValidationException>(() => monitor.Get("bad"));
    }

    [Fact]
    public void AddOrionOptions_without_a_delegate_still_wires_validation()
    {
        var services = new ServiceCollection();
        services.AddOrionOptions<LeaseOptions>();

        using var provider = services.BuildServiceProvider();

        // The default LeaseDuration is zero, so validation must reject the unconfigured type.
        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<LeaseOptions>>().Value);
    }

    [Fact]
    public void AddOrionOptions_returns_a_builder_for_further_chaining()
    {
        var services = new ServiceCollection();

        var builder = services.AddOrionOptions<LeaseOptions>(o => o.LeaseDuration = TimeSpan.FromSeconds(1));
        builder.PostConfigure(o => o.Name = "post");

        using var provider = services.BuildServiceProvider();
        Assert.Equal("post", provider.GetRequiredService<IOptions<LeaseOptions>>().Value.Name);
    }

    private sealed class BareOptions : OrionOptions;

    private sealed class LeaseOptions : OrionOptions
    {
        public TimeSpan LeaseDuration { get; set; }

        public string Name { get; set; } = "default";

        public override void Validate(OrionOptionsValidationContext context)
        {
            base.Validate(context);
            context.RequirePositive(LeaseDuration, nameof(LeaseDuration));
            context.RequireNotNullOrWhiteSpace(Name, nameof(Name));
        }
    }
}
