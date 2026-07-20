namespace Moongazing.Orion.Abstractions.Results;

/// <summary>
/// The contract an Orion outcome type satisfies: it either succeeded, or it carries an
/// <see cref="OrionError"/> explaining why not.
/// </summary>
/// <remarks>
/// <para>
/// Contract only. The spine defines the shape so packages can hand back outcomes - and so
/// cross-cutting code (telemetry stamping, <c>ProblemDetails</c> mapping, envelope rendering)
/// can read any Orion result without knowing which package produced it. The ergonomic type
/// with the map/bind/match surface ships in the <c>OrionResult</c> package.
/// </para>
/// <para>
/// Implementations must keep <see cref="IsSuccess"/> and <see cref="Error"/> consistent:
/// <see cref="Error"/> is non-null exactly when <see cref="IsSuccess"/> is
/// <see langword="false"/>.
/// </para>
/// </remarks>
public interface IOrionResult
{
    /// <summary>True when the operation achieved its intent.</summary>
    bool IsSuccess { get; }

    /// <summary>
    /// The failure, or <see langword="null"/> when <see cref="IsSuccess"/> is
    /// <see langword="true"/>.
    /// </summary>
#pragma warning disable CA1716 // 'Error' is a VB keyword, but it is also the universal name for
                              // this member across .NET result libraries. Renaming it would cost
                              // every C# consumer discoverability to spare a VB implementer one
                              // bracketed identifier - and this name is frozen at 1.0.
    OrionError? Error { get; }
#pragma warning restore CA1716
}

/// <summary>
/// An <see cref="IOrionResult"/> that carries a value on success.
/// </summary>
/// <typeparam name="TValue">The success value type.</typeparam>
/// <remarks>
/// <see cref="Value"/> is meaningful only when <see cref="IOrionResult.IsSuccess"/> is
/// <see langword="true"/>; an implementation is free to throw when it is read on a failure.
/// </remarks>
public interface IOrionResult<out TValue> : IOrionResult
{
    /// <summary>The success value. Only meaningful when the result succeeded.</summary>
    TValue Value { get; }
}
