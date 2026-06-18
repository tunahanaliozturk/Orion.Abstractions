namespace Moongazing.Orion.Abstractions.Demo;

using Microsoft.Extensions.DependencyInjection;
using Moongazing.Orion.Abstractions;
using Moongazing.Orion.Abstractions.Time;

/// <summary>
/// Demonstrates <see cref="OrionAbstractionsServiceCollectionExtensions.AddOrionAbstractions"/>:
/// it registers <see cref="IOrionClock"/> via TryAdd, so it is safe to call from multiple
/// Orion packages and a consumer override always wins.
/// </summary>
internal sealed class DependencyInjectionDemo
{
    public void Run()
    {
        ConsoleUi.Step("1) Default registration resolves the production SystemOrionClock:");
        var services = new ServiceCollection();
        services.AddOrionAbstractions();
        using (var provider = services.BuildServiceProvider())
        {
            var clock = provider.GetRequiredService<IOrionClock>();
            ConsoleUi.Step($"   resolved IOrionClock -> {clock.GetType().Name}");
        }

        ConsoleUi.Step("2) Calling AddOrionAbstractions twice is safe (TryAdd, no duplicate):");
        var idempotent = new ServiceCollection();
        idempotent.AddOrionAbstractions();
        idempotent.AddOrionAbstractions();
        var clockRegistrations = idempotent.Count(d => d.ServiceType == typeof(IOrionClock));
        ConsoleUi.Step($"   IOrionClock registration count = {clockRegistrations} (expected 1)");

        ConsoleUi.Step("3) A consumer override registered FIRST wins (TryAdd no-ops):");
        var custom = new ServiceCollection();
        var fixedInstant = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        custom.AddSingleton<IOrionClock>(new SystemOrionClock(new FixedTimeProvider(fixedInstant)));
        custom.AddOrionAbstractions();
        using (var provider = custom.BuildServiceProvider())
        {
            var clock = provider.GetRequiredService<IOrionClock>();
            ConsoleUi.Step($"   resolved IOrionClock.UtcNow = {clock.UtcNow:O} (the override, not the default)");
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset instant;

        public FixedTimeProvider(DateTimeOffset instant) => this.instant = instant;

        public override DateTimeOffset GetUtcNow() => instant;
    }
}
