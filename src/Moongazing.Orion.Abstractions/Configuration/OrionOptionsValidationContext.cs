namespace Moongazing.Orion.Abstractions.Configuration;

using System.Globalization;

/// <summary>
/// The collector an <see cref="OrionOptions.Validate(OrionOptionsValidationContext)"/>
/// implementation reports invariant violations to. It owns the failure-message format so
/// every package in the family fails configuration the same way:
/// <c>OrionLockOptions.LeaseDuration: must be greater than zero (was -00:00:01).</c>
/// </summary>
/// <remarks>
/// Instances are created by the validation wiring, are single-threaded, and are not reused
/// across validation passes.
/// </remarks>
public sealed class OrionOptionsValidationContext
{
    private readonly List<string> failures = [];

    internal OrionOptionsValidationContext(Type optionsType, string? optionsName)
    {
        ArgumentNullException.ThrowIfNull(optionsType);
        OptionsType = optionsType;
        OptionsName = optionsName;
    }

    /// <summary>The options type being validated.</summary>
    public Type OptionsType { get; }

    /// <summary>
    /// The named-options name being validated, or <see langword="null"/> for the default
    /// (unnamed) instance. Surfaced so a validator can relax an invariant for a specific
    /// named configuration.
    /// </summary>
    public string? OptionsName { get; }

    /// <summary>True once at least one failure has been reported.</summary>
    public bool HasFailures => failures.Count > 0;

    /// <summary>The failures reported so far, in report order.</summary>
    public IReadOnlyList<string> Failures => failures;

    /// <summary>
    /// Report a failure against a specific setting. The message is prefixed with
    /// <c>{OptionsType}.{propertyName}</c> so the host operator can find the setting without
    /// reading the library's source.
    /// </summary>
    /// <param name="propertyName">The offending property, e.g. <c>LeaseDuration</c>.</param>
    /// <param name="reason">Why it is invalid, e.g. <c>must be greater than zero</c>.</param>
    public void AddFailure(string propertyName, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        failures.Add($"{OptionsType.Name}.{propertyName}: {reason}");
    }

    /// <summary>
    /// Report a failure that spans several settings (a cross-property invariant) and so has no
    /// single owning property.
    /// </summary>
    /// <param name="reason">Why the configuration is invalid.</param>
    public void AddFailure(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        failures.Add($"{OptionsType.Name}: {reason}");
    }

    /// <summary>
    /// Report a failure when <paramref name="condition"/> is <see langword="false"/>. The
    /// inverse of an assertion: the condition states what must hold.
    /// </summary>
    /// <param name="condition">The invariant that must hold.</param>
    /// <param name="reason">Why the configuration is invalid when it does not.</param>
    public void Require(bool condition, string reason)
    {
        if (!condition)
        {
            AddFailure(reason);
        }
    }

    /// <summary>Require a string setting to be non-null and not whitespace.</summary>
    /// <param name="value">The configured value.</param>
    /// <param name="propertyName">The property being validated.</param>
    public void RequireNotNullOrWhiteSpace(string? value, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            AddFailure(propertyName, value is null ? "must be set." : "must not be empty or whitespace.");
        }
    }

    /// <summary>Require a duration to be strictly greater than zero.</summary>
    /// <param name="value">The configured value.</param>
    /// <param name="propertyName">The property being validated.</param>
    public void RequirePositive(TimeSpan value, string propertyName)
    {
        if (value <= TimeSpan.Zero)
        {
            AddFailure(propertyName, Invariant($"must be greater than zero (was {value})."));
        }
    }

    /// <summary>Require a count to be strictly greater than zero.</summary>
    /// <param name="value">The configured value.</param>
    /// <param name="propertyName">The property being validated.</param>
    public void RequirePositive(int value, string propertyName)
    {
        if (value <= 0)
        {
            AddFailure(propertyName, Invariant($"must be greater than zero (was {value})."));
        }
    }

    /// <summary>Require a count to be zero or greater.</summary>
    /// <param name="value">The configured value.</param>
    /// <param name="propertyName">The property being validated.</param>
    public void RequireNonNegative(int value, string propertyName)
    {
        if (value < 0)
        {
            AddFailure(propertyName, Invariant($"must not be negative (was {value})."));
        }
    }

    /// <summary>Require a duration to be zero or greater.</summary>
    /// <param name="value">The configured value.</param>
    /// <param name="propertyName">The property being validated.</param>
    public void RequireNonNegative(TimeSpan value, string propertyName)
    {
        if (value < TimeSpan.Zero)
        {
            AddFailure(propertyName, Invariant($"must not be negative (was {value})."));
        }
    }

    /// <summary>Require a count to fall within an inclusive range.</summary>
    /// <param name="value">The configured value.</param>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The inclusive upper bound.</param>
    /// <param name="propertyName">The property being validated.</param>
    public void RequireInRange(int value, int min, int max, string propertyName)
    {
        if (value < min || value > max)
        {
            AddFailure(propertyName, Invariant($"must be between {min} and {max} inclusive (was {value})."));
        }
    }

    /// <summary>Require a duration to fall within an inclusive range.</summary>
    /// <param name="value">The configured value.</param>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The inclusive upper bound.</param>
    /// <param name="propertyName">The property being validated.</param>
    public void RequireInRange(TimeSpan value, TimeSpan min, TimeSpan max, string propertyName)
    {
        if (value < min || value > max)
        {
            AddFailure(propertyName, Invariant($"must be between {min} and {max} inclusive (was {value})."));
        }
    }

    /// <summary>
    /// The reported failures rendered as one operator-facing message, or
    /// <see langword="null"/> when nothing failed.
    /// </summary>
    internal string? BuildMessage()
    {
        if (failures.Count == 0)
        {
            return null;
        }

        var target = OptionsName is null
            ? OptionsType.Name
            : $"{OptionsType.Name} (named '{OptionsName}')";

        return failures.Count == 1
            ? $"{target} is invalid. {failures[0]}"
            : $"{target} is invalid:{Environment.NewLine}  - {string.Join($"{Environment.NewLine}  - ", failures)}";
    }

    private static string Invariant(FormattableString message) =>
        message.ToString(CultureInfo.InvariantCulture);
}
