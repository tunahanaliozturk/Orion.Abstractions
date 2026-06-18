namespace Moongazing.Orion.Abstractions.Demo;

using Moongazing.Orion.Abstractions.Observers;

/// <summary>
/// Demonstrates <see cref="SafeObserverInvoker"/>: a null observer is a no-op, a faulting
/// observer is swallowed (so an observability outage cannot break the load-bearing path),
/// cancellation still propagates, and resolution can happen inside the guard.
/// </summary>
internal sealed class SafeObserverDemo
{
    /// <summary>A tiny observer contract, like one an Orion library would expose to consumers.</summary>
    private interface IProgressObserver
    {
        void OnProgress(int percent);

        Task OnCompletedAsync(CancellationToken cancellationToken);
    }

    private sealed class GoodObserver : IProgressObserver
    {
        public void OnProgress(int percent) => ConsoleUi.Step($"observer received progress: {percent}%");

        public Task OnCompletedAsync(CancellationToken cancellationToken)
        {
            ConsoleUi.Step("observer received async completion");
            return Task.CompletedTask;
        }
    }

    private sealed class FaultingObserver : IProgressObserver
    {
        public void OnProgress(int percent) => throw new InvalidOperationException("observer is broken");

        public Task OnCompletedAsync(CancellationToken cancellationToken)
            => throw new InvalidOperationException("async observer is broken");
    }

    public async Task RunAsync()
    {
        ConsoleUi.Step("1) A null observer is simply skipped (no NullReferenceException):");
        SafeObserverInvoker.Invoke<IProgressObserver>(
            observer: null,
            action: o => o.OnProgress(10));
        ConsoleUi.Step("   -> host path continued, nothing was invoked");

        ConsoleUi.Step("2) A healthy observer is invoked normally:");
        SafeObserverInvoker.Invoke<IProgressObserver>(
            observer: new GoodObserver(),
            action: o => o.OnProgress(50));

        ConsoleUi.Step("3) A faulting observer is swallowed and reported via onFault:");
        SafeObserverInvoker.Invoke<IProgressObserver>(
            observer: new FaultingObserver(),
            action: o => o.OnProgress(75),
            onFault: ex => ConsoleUi.Step($"   -> swallowed fault reported: {ex.Message} (host continued)"));

        ConsoleUi.Step("4) Async: a healthy observer awaited to completion:");
        await SafeObserverInvoker.InvokeAsync<IProgressObserver>(
            observer: new GoodObserver(),
            action: o => o.OnCompletedAsync(CancellationToken.None));

        ConsoleUi.Step("5) Async: a faulting observer is swallowed, not propagated:");
        await SafeObserverInvoker.InvokeAsync<IProgressObserver>(
            observer: new FaultingObserver(),
            action: o => o.OnCompletedAsync(CancellationToken.None),
            onFault: ex => ConsoleUi.Step($"   -> swallowed async fault: {ex.Message}"));

        ConsoleUi.Step("6) Cancellation ALWAYS propagates (never downgraded to a swallowed warning):");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        try
        {
            await SafeObserverInvoker.InvokeAsync<IProgressObserver>(
                observer: new GoodObserver(),
                action: async o =>
                {
                    o.GetType();
                    cts.Token.ThrowIfCancellationRequested();
                    await Task.CompletedTask;
                },
                onFault: _ => ConsoleUi.Step("   -> THIS SHOULD NOT PRINT (cancellation is not a fault)"),
                cancellationToken: cts.Token);
        }
        catch (OperationCanceledException)
        {
            ConsoleUi.Step("   -> OperationCanceledException propagated as expected");
        }

        ConsoleUi.Step("7) Resolve(...) puts a throwing observer ctor INSIDE the guard:");
        SafeObserverInvoker.Resolve<IProgressObserver>(
            resolve: () => throw new InvalidOperationException("ctor / DI resolution threw"),
            action: o => o.OnProgress(100),
            onFault: ex => ConsoleUi.Step($"   -> resolution fault swallowed: {ex.Message} (host path safe)"));
    }
}
