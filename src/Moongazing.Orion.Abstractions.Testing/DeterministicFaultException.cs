namespace Moongazing.Orion.Abstractions.Testing;

using System;

/// <summary>
/// The default exception a <see cref="DeterministicFaultInjector"/> throws when an attempt is
/// scheduled to fail. A dedicated type so a test can catch injected faults distinctly from any
/// real exception the code under test might raise.
/// </summary>
public sealed class DeterministicFaultException : Exception
{
    /// <summary>Create the exception with the default message.</summary>
    public DeterministicFaultException()
        : base("Injected deterministic fault.")
    {
    }

    /// <summary>Create the exception with a custom <paramref name="message"/>.</summary>
    /// <param name="message">The message.</param>
    public DeterministicFaultException(string message)
        : base(message)
    {
    }

    /// <summary>Create the exception with a custom <paramref name="message"/> and <paramref name="innerException"/>.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The inner exception.</param>
    public DeterministicFaultException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
