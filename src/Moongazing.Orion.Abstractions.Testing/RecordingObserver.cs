namespace Moongazing.Orion.Abstractions.Testing;

using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// A recording test double for <c>SafeObserverInvoker</c> call sites. It captures every
/// observer invocation and every swallowed fault so a test can assert that an observer (and
/// the host that drives it) behaves per the observer contract: invocations happen in order,
/// a fault is swallowed rather than propagated, and cancellation is not downgraded to a
/// recorded fault.
/// <para>
/// Compose it with <c>SafeObserverInvoker</c> by passing <see cref="Track(System.Action{TObserver})"/>
/// (or <see cref="TrackAsync(System.Func{TObserver, Task})"/>) as the action and
/// <see cref="OnFault"/> as the fault hook. The double never throws from the action or the
/// fault hook itself, so it cannot perturb the very fault-safety it is used to verify.
/// </para>
/// </summary>
/// <typeparam name="TObserver">The observer type passed to <c>SafeObserverInvoker</c>.</typeparam>
public sealed class RecordingObserver<TObserver>
    where TObserver : class
{
    private readonly object gate = new();
    private readonly List<TObserver> invocations = new();
    private readonly List<Exception> faults = new();

    /// <summary>
    /// The observer instance handed to the recorded action, or <see langword="null"/> to drive
    /// the no-op (null-observer) path. Supply a real or stub observer when the action under test
    /// needs to call members on it; leave it null to exercise the skip-on-null behavior.
    /// </summary>
    public TObserver? Observer { get; }

    /// <summary>Create a recording double with no backing observer (the null / no-op case).</summary>
    public RecordingObserver()
    {
    }

    /// <summary>Create a recording double that hands <paramref name="observer"/> to the recorded action.</summary>
    /// <param name="observer">The observer instance the action receives, or null for the no-op path.</param>
    public RecordingObserver(TObserver? observer)
    {
        Observer = observer;
    }

    /// <summary>The observer instances passed to the action, in invocation order. Empty if never invoked.</summary>
    public IReadOnlyList<TObserver> Invocations
    {
        get
        {
            lock (gate)
            {
                return invocations.ToArray();
            }
        }
    }

    /// <summary>The faults reported through <see cref="OnFault"/>, in order. Empty if none were reported.</summary>
    public IReadOnlyList<Exception> Faults
    {
        get
        {
            lock (gate)
            {
                return faults.ToArray();
            }
        }
    }

    /// <summary>The number of times the recorded action ran to completion.</summary>
    public int InvocationCount
    {
        get
        {
            lock (gate)
            {
                return invocations.Count;
            }
        }
    }

    /// <summary>The number of faults reported through <see cref="OnFault"/>.</summary>
    public int FaultCount
    {
        get
        {
            lock (gate)
            {
                return faults.Count;
            }
        }
    }

    /// <summary>True if at least one invocation ran to completion.</summary>
    public bool WasInvoked => InvocationCount > 0;

    /// <summary>True if at least one fault was reported.</summary>
    public bool Faulted => FaultCount > 0;

    /// <summary>
    /// The single fault reported, asserting exactly one was. Use this when a test expects one
    /// swallowed fault and wants to inspect it.
    /// </summary>
    /// <returns>The one reported fault.</returns>
    /// <exception cref="InvalidOperationException">No fault, or more than one, was reported.</exception>
    public Exception SingleFault()
    {
        lock (gate)
        {
            return faults.Count switch
            {
                1 => faults[0],
                0 => throw new InvalidOperationException("Expected exactly one observer fault, but none were recorded."),
                _ => throw new InvalidOperationException(
                    $"Expected exactly one observer fault, but {faults.Count} were recorded."),
            };
        }
    }

    /// <summary>
    /// The fault hook to hand to <c>SafeObserverInvoker</c> as its <c>onFault</c> argument. It
    /// records the swallowed exception and never throws.
    /// </summary>
    public Action<Exception> OnFault => Record;

    /// <summary>
    /// Wrap a synchronous observer action so each completed invocation is recorded. Pass the
    /// result as the action argument to <c>SafeObserverInvoker.Invoke</c> or
    /// <c>SafeObserverInvoker.Resolve</c>. The invocation is recorded only after
    /// <paramref name="action"/> returns, so a faulting action records a fault (via
    /// <see cref="OnFault"/>) but not an invocation, matching what the contract guarantees.
    /// </summary>
    /// <param name="action">The action under test; may be null to record the invocation only.</param>
    /// <returns>An action suitable for <c>SafeObserverInvoker</c>.</returns>
    public Action<TObserver> Track(Action<TObserver>? action = null)
    {
        return observer =>
        {
            action?.Invoke(observer);
            RecordInvocation(observer);
        };
    }

    /// <summary>
    /// Wrap an asynchronous observer action so each completed invocation is recorded. Pass the
    /// result as the action argument to <c>SafeObserverInvoker.InvokeAsync</c>. The invocation is
    /// recorded only after the returned task completes successfully, so a faulting or cancelled
    /// action records no invocation.
    /// </summary>
    /// <param name="action">The async action under test; may be null to record the invocation only.</param>
    /// <returns>An async action suitable for <c>SafeObserverInvoker.InvokeAsync</c>.</returns>
    public Func<TObserver, Task> TrackAsync(Func<TObserver, Task>? action = null)
    {
        return async observer =>
        {
            if (action is not null)
            {
                await action(observer).ConfigureAwait(false);
            }
            RecordInvocation(observer);
        };
    }

    /// <summary>Clear all recorded invocations and faults, so the double can be reused across phases of a test.</summary>
    public void Reset()
    {
        lock (gate)
        {
            invocations.Clear();
            faults.Clear();
        }
    }

    private void RecordInvocation(TObserver observer)
    {
        lock (gate)
        {
            invocations.Add(observer);
        }
    }

    private void Record(Exception fault)
    {
        ArgumentNullException.ThrowIfNull(fault);
        lock (gate)
        {
            faults.Add(fault);
        }
    }
}
