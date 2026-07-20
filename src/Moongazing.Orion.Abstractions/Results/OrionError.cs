namespace Moongazing.Orion.Abstractions.Results;

/// <summary>
/// The family's error value: a machine-readable <see cref="Code"/>, a human-readable
/// <see cref="Message"/>, and an optional <see cref="Target"/> naming what the error is about.
/// </summary>
/// <remarks>
/// <para>
/// This is the vocabulary half of the result contract. It is a value type with no exception,
/// no stack trace, and no allocation on the success path - so a package can report an expected
/// outcome (a lock already held, an idempotency key replayed, a key expired) without the cost
/// and control-flow damage of throwing. Genuinely exceptional conditions still throw.
/// </para>
/// <para>
/// <see cref="OrionErrorCodes"/> holds the canonical codes; a package may define its own, but
/// should prefer a canonical one so consumers can handle errors across packages uniformly and
/// <c>OrionEnvelope</c> can map them to HTTP without a per-package table.
/// </para>
/// </remarks>
public readonly record struct OrionError
{
    /// <summary>Create an error.</summary>
    /// <param name="code">
    /// The stable machine-readable code, e.g. <see cref="OrionErrorCodes.Conflict"/>.
    /// Lowercase <c>snake_case</c> by convention; never localised, never reworded.
    /// </param>
    /// <param name="message">The human-readable description, safe to log.</param>
    /// <param name="target">
    /// Optional: what the error is about - a field name, a resource id, a lock key.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="code"/> or <paramref name="message"/> is null, empty, or whitespace.
    /// </exception>
    public OrionError(string code, string message, string? target = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code;
        Message = message;
        Target = target;
    }

    /// <summary>The stable machine-readable code consumers branch on.</summary>
    public string Code { get; }

    /// <summary>The human-readable description. Safe to log; not a UI string.</summary>
    public string Message { get; }

    /// <summary>What the error is about - a field, a resource id, a key - or null.</summary>
    public string? Target { get; }

    /// <summary>
    /// Renders as <c>code: message</c>, or <c>code (target): message</c> when a target is set.
    /// </summary>
    public override string ToString() =>
        Target is null ? $"{Code}: {Message}" : $"{Code} ({Target}): {Message}";
}
