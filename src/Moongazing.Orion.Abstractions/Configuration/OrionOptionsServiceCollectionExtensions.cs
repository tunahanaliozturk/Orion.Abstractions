namespace Moongazing.Orion.Abstractions.Configuration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <summary>
/// The registration half of the Orion options convention: one call that configures an
/// <see cref="OrionOptions"/>-derived type and wires its validation.
/// </summary>
public static class OrionOptionsServiceCollectionExtensions
{
    /// <summary>
    /// Configure <typeparamref name="TOptions"/> and register its validator. This is what an
    /// <c>AddOrionX()</c> method calls; it never registers the same validator twice, so
    /// calling <c>AddOrionX()</c> more than once is safe.
    /// </summary>
    /// <typeparam name="TOptions">The options type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">
    /// Optional configuration delegate. Omit it to register validation for options bound
    /// elsewhere (for example from <c>IConfiguration</c>).
    /// </param>
    /// <param name="name">
    /// The named-options name, or <see langword="null"/> for the default instance.
    /// </param>
    /// <returns>
    /// The <see cref="OptionsBuilder{TOptions}"/>, so a caller can chain further configuration
    /// (including <c>ValidateOnStart()</c> where the hosting package is referenced).
    /// </returns>
    public static OptionsBuilder<TOptions> AddOrionOptions<TOptions>(
        this IServiceCollection services,
        Action<TOptions>? configure = null,
        string? name = null)
        where TOptions : OrionOptions
    {
        ArgumentNullException.ThrowIfNull(services);

        var builder = services.AddOptions<TOptions>(name ?? Options.DefaultName);
        if (configure is not null)
        {
            builder.Configure(configure);
        }

        // The validator is stateless and validates every name, so one registration covers all
        // named instances of TOptions - TryAddEnumerable keeps repeat AddOrionX() calls idempotent.
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<TOptions>, OrionOptionsValidator<TOptions>>());

        return builder;
    }
}
