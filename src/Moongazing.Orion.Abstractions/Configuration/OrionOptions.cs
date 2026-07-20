namespace Moongazing.Orion.Abstractions.Configuration;

/// <summary>
/// The base every <c>OrionXOptions</c> derives from. It exists so the family has ONE options
/// shape instead of sixteen: a package declares its settings as properties, expresses its
/// invariants once in <see cref="Validate(OrionOptionsValidationContext)"/>, and gets
/// consistent validation wiring (and consistent failure messages) from
/// <c>AddOrionOptions&lt;TOptions&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// Validation is collecting, not fail-fast: an implementation reports <em>every</em> invalid
/// setting through the context rather than throwing on the first one, so a misconfigured host
/// fails once with a complete list instead of one round-trip per mistake.
/// </para>
/// <para>
/// The default implementation is a no-op, so an options type with no invariants needs no
/// override. Options types should be mutable classes with parameterless constructors and
/// sensible defaults - they are bound from configuration by
/// <c>Microsoft.Extensions.Options</c>, which cannot bind an immutable record without a
/// matching constructor shape.
/// </para>
/// </remarks>
public abstract class OrionOptions
{
    /// <summary>
    /// Validate this instance, reporting each invariant violation to
    /// <paramref name="context"/>. Report every failure; do not throw and do not stop at the
    /// first one. The default implementation reports nothing.
    /// </summary>
    /// <param name="context">The collector failures are reported to.</param>
    /// <remarks>
    /// Public rather than protected on purpose: a protected member would force every package
    /// author outside this assembly into the <c>protected internal</c> override trap (CS0507),
    /// and a public one lets a test assert an options type's invariants directly without going
    /// through a service provider.
    /// </remarks>
    public virtual void Validate(OrionOptionsValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
    }
}
