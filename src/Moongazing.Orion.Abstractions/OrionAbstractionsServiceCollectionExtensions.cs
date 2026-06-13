namespace Moongazing.Orion.Abstractions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moongazing.Orion.Abstractions.Time;

/// <summary>DI registration helpers for the shared Orion abstractions.</summary>
public static class OrionAbstractionsServiceCollectionExtensions
{
    /// <summary>
    /// Register the shared Orion abstractions. Currently registers
    /// <see cref="IOrionClock"/> as a <see cref="SystemOrionClock"/> singleton if no clock
    /// is already registered. Safe to call from multiple Orion packages' Add methods;
    /// uses TryAdd so the first registration (or a consumer override) wins.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection for chaining.</returns>
    public static IServiceCollection AddOrionAbstractions(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IOrionClock, SystemOrionClock>();
        return services;
    }
}
