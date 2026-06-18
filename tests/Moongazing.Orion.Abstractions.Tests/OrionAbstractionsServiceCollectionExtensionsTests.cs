namespace Moongazing.Orion.Abstractions.Tests;

using Microsoft.Extensions.DependencyInjection;
using Moongazing.Orion.Abstractions.Time;
using Xunit;

public sealed class OrionAbstractionsServiceCollectionExtensionsTests
{
    [Fact]
    public void AddOrionAbstractions_registers_the_system_clock()
    {
        var services = new ServiceCollection();
        services.AddOrionAbstractions();

        using var provider = services.BuildServiceProvider();
        var clock = provider.GetService<IOrionClock>();

        Assert.IsType<SystemOrionClock>(clock);
    }

    [Fact]
    public void AddOrionAbstractions_registers_the_clock_as_a_singleton()
    {
        var services = new ServiceCollection();
        services.AddOrionAbstractions();

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IOrionClock>();
        var second = provider.GetRequiredService<IOrionClock>();

        Assert.Same(first, second);
    }

    [Fact]
    public void AddOrionAbstractions_returns_the_same_collection_for_chaining()
    {
        var services = new ServiceCollection();
        var returned = services.AddOrionAbstractions();
        Assert.Same(services, returned);
    }

    [Fact]
    public void AddOrionAbstractions_rejects_a_null_service_collection()
    {
        Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddOrionAbstractions());
    }

    [Fact]
    public void AddOrionAbstractions_is_idempotent_across_repeated_calls()
    {
        var services = new ServiceCollection();
        services.AddOrionAbstractions();
        services.AddOrionAbstractions();

        // TryAdd means a second call must not register a duplicate descriptor.
        Assert.Single(services, d => d.ServiceType == typeof(IOrionClock));
    }

    [Fact]
    public void AddOrionAbstractions_does_not_override_a_consumer_registration()
    {
        var custom = new SystemOrionClock(TimeProvider.System);
        var services = new ServiceCollection();

        // Consumer registers their own clock first; TryAdd must leave it in place.
        services.AddSingleton<IOrionClock>(custom);
        services.AddOrionAbstractions();

        using var provider = services.BuildServiceProvider();
        Assert.Same(custom, provider.GetRequiredService<IOrionClock>());
    }
}
