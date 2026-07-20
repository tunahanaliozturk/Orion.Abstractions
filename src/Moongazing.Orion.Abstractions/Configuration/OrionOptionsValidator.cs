namespace Moongazing.Orion.Abstractions.Configuration;

using Microsoft.Extensions.Options;

/// <summary>
/// Bridges <see cref="OrionOptions.Validate(OrionOptionsValidationContext)"/> onto
/// <see cref="IValidateOptions{TOptions}"/>, so an options type expresses its invariants once
/// and the standard <c>Microsoft.Extensions.Options</c> pipeline enforces them.
/// </summary>
/// <typeparam name="TOptions">The options type being validated.</typeparam>
/// <remarks>
/// Registered by <c>AddOrionOptions&lt;TOptions&gt;</c>. Registering it directly is supported
/// but unnecessary.
/// </remarks>
public sealed class OrionOptionsValidator<TOptions> : IValidateOptions<TOptions>
    where TOptions : OrionOptions
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, TOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Options.DefaultName is the empty string; report it as "the default instance" (null)
        // so a failure message for the unnamed registration does not read "named ''".
        var context = new OrionOptionsValidationContext(
            typeof(TOptions),
            string.IsNullOrEmpty(name) ? null : name);

        options.Validate(context);

        var message = context.BuildMessage();
        return message is null
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(message);
    }
}
