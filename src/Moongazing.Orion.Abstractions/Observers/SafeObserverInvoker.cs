namespace Moongazing.Orion.Abstractions.Observers;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Fault-safe invocation helpers for consumer-supplied observer hooks - the convention
/// every Orion observer follows since OrionPatch v0.2.18:
/// <list type="bullet">
/// <item>A <see langword="null"/> observer is a no-op (the call site is skipped).</item>
/// <item>An observer fault is swallowed so an observability outage can never break the
/// load-bearing path the observer is attached to.</item>
/// <item><see cref="OperationCanceledException"/> ALWAYS propagates when the supplied token
/// is cancelled, so cooperative shutdown is never downgraded to a swallowed warning.</item>
/// </list>
/// Centralising this removes the bespoke copies that historically drifted (double-fire under
/// race, resolution outside the guard, cancellation downgrade).
/// </summary>
public static class SafeObserverInvoker
{
    /// <summary>
    /// Invoke a synchronous observer action with the Orion fault-safe contract. The
    /// <paramref name="onFault"/> callback (if supplied) is invoked with a swallowed
    /// exception for logging; it must not throw.
    /// </summary>
    /// <typeparam name="TObserver">The observer type.</typeparam>
    /// <param name="observer">The observer, or null for a no-op.</param>
    /// <param name="action">The callback to invoke on the observer.</param>
    /// <param name="onFault">Optional logging hook for a swallowed fault.</param>
    public static void Invoke<TObserver>(TObserver? observer, Action<TObserver> action, Action<Exception>? onFault = null)
        where TObserver : class
    {
        ArgumentNullException.ThrowIfNull(action);
        if (observer is null)
        {
            return;
        }
        try
        {
            action(observer);
        }
#pragma warning disable CA1031 // observer is observability, not load-bearing
        catch (Exception ex)
#pragma warning restore CA1031
        {
            onFault?.Invoke(ex);
        }
    }

    /// <summary>
    /// Invoke an asynchronous observer callback with the Orion fault-safe contract.
    /// <see cref="OperationCanceledException"/> propagates when <paramref name="cancellationToken"/>
    /// is cancelled; all other faults are swallowed (and passed to <paramref name="onFault"/>).
    /// </summary>
    /// <typeparam name="TObserver">The observer type.</typeparam>
    /// <param name="observer">The observer, or null for a no-op.</param>
    /// <param name="action">The async callback to invoke on the observer.</param>
    /// <param name="onFault">Optional logging hook for a swallowed fault.</param>
    /// <param name="cancellationToken">The ambient cancellation token.</param>
    public static async Task InvokeAsync<TObserver>(
        TObserver? observer,
        Func<TObserver, Task> action,
        Action<Exception>? onFault = null,
        CancellationToken cancellationToken = default)
        where TObserver : class
    {
        ArgumentNullException.ThrowIfNull(action);
        if (observer is null)
        {
            return;
        }
        try
        {
            await action(observer).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Cooperative shutdown is never a swallowed observer fault.
            throw;
        }
#pragma warning disable CA1031 // observer is observability, not load-bearing
        catch (Exception ex)
#pragma warning restore CA1031
        {
            onFault?.Invoke(ex);
        }
    }

    /// <summary>
    /// Resolve an observer from a factory and invoke it, with the resolution itself INSIDE
    /// the fault guard. This closes the gap where a registered observer whose construction
    /// (or DI dependency) throws would abort the host path at resolution time - the OrionAudit
    /// v0.7.26 fix generalised. <paramref name="resolve"/> may return null for 'no observer'.
    /// </summary>
    /// <typeparam name="TObserver">The observer type.</typeparam>
    /// <param name="resolve">A factory that resolves the observer (may throw / return null).</param>
    /// <param name="action">The callback to invoke on the resolved observer.</param>
    /// <param name="onFault">Optional logging hook for a swallowed fault.</param>
    public static void Resolve<TObserver>(Func<TObserver?> resolve, Action<TObserver> action, Action<Exception>? onFault = null)
        where TObserver : class
    {
        ArgumentNullException.ThrowIfNull(resolve);
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            var observer = resolve();
            if (observer is null)
            {
                return;
            }
            action(observer);
        }
#pragma warning disable CA1031 // observer is observability, not load-bearing
        catch (Exception ex)
#pragma warning restore CA1031
        {
            onFault?.Invoke(ex);
        }
    }
}
