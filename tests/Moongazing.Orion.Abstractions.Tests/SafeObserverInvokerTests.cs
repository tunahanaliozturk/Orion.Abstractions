namespace Moongazing.Orion.Abstractions.Tests;

using Moongazing.Orion.Abstractions.Observers;
using Xunit;

public sealed class SafeObserverInvokerTests
{
    private interface IThing { void Do(); }

    [Fact]
    public void Invoke_null_observer_is_a_noop()
    {
        // Must not throw.
        SafeObserverInvoker.Invoke<IThing>(null, t => t.Do());
    }

    [Fact]
    public void Invoke_calls_the_observer()
    {
        var called = false;
        SafeObserverInvoker.Invoke(new object(), _ => called = true);
        Assert.True(called);
    }

    [Fact]
    public void Invoke_swallows_observer_fault_and_reports_it()
    {
        Exception? captured = null;
        SafeObserverInvoker.Invoke(new object(),
            _ => throw new InvalidOperationException("boom"),
            onFault: ex => captured = ex);
        Assert.IsType<InvalidOperationException>(captured);
    }

    [Fact]
    public async Task InvokeAsync_swallows_fault_but_propagates_cancellation()
    {
        // Swallowed fault.
        Exception? captured = null;
        await SafeObserverInvoker.InvokeAsync(new object(),
            _ => throw new InvalidOperationException("boom"),
            onFault: ex => captured = ex);
        Assert.IsType<InvalidOperationException>(captured);

        // Cancellation propagates when the token is cancelled.
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            SafeObserverInvoker.InvokeAsync<object>(new object(),
                _ => throw new OperationCanceledException(cts.Token),
                cancellationToken: cts.Token));
    }

    [Fact]
    public void Resolve_swallows_a_throwing_factory()
    {
        Exception? captured = null;
        SafeObserverInvoker.Resolve<object>(
            () => throw new InvalidOperationException("ctor threw"),
            _ => Assert.Fail("action must not run when resolve throws"),
            onFault: ex => captured = ex);
        Assert.IsType<InvalidOperationException>(captured);
    }

    [Fact]
    public void Resolve_null_factory_result_skips_the_action()
    {
        var ran = false;
        SafeObserverInvoker.Resolve<object>(() => null, _ => ran = true);
        Assert.False(ran);
    }

    [Fact]
    public void Invoke_passes_the_observer_instance_to_the_action()
    {
        var observer = new object();
        object? received = null;
        SafeObserverInvoker.Invoke(observer, o => received = o);
        Assert.Same(observer, received);
    }

    [Fact]
    public void Invoke_null_action_throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            SafeObserverInvoker.Invoke<object>(new object(), null!));
    }

    [Fact]
    public void Invoke_null_action_throws_even_for_a_null_observer()
    {
        // The action null-check runs before the observer null no-op short-circuit.
        Assert.Throws<ArgumentNullException>(() =>
            SafeObserverInvoker.Invoke<object>(null, null!));
    }

    [Fact]
    public void Invoke_swallowed_fault_without_onFault_does_not_throw()
    {
        // No onFault supplied: the fault is still swallowed (not rethrown).
        SafeObserverInvoker.Invoke(new object(), _ => throw new InvalidOperationException("boom"));
    }

    [Fact]
    public async Task InvokeAsync_null_observer_is_a_noop()
    {
        await SafeObserverInvoker.InvokeAsync<IThing>(null, t => { t.Do(); return Task.CompletedTask; });
    }

    [Fact]
    public async Task InvokeAsync_null_action_throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            SafeObserverInvoker.InvokeAsync<object>(new object(), null!));
    }

    [Fact]
    public async Task InvokeAsync_awaits_the_observer_callback()
    {
        var observer = new object();
        object? received = null;
        await SafeObserverInvoker.InvokeAsync(observer, async o =>
        {
            await Task.Yield();
            received = o;
        });
        Assert.Same(observer, received);
    }

    [Fact]
    public async Task InvokeAsync_swallows_an_OperationCanceledException_when_the_token_is_not_cancelled()
    {
        // The cancellation re-throw is guarded by token.IsCancellationRequested. An OCE that is
        // NOT driven by the supplied token is treated as an ordinary swallowed observer fault.
        Exception? captured = null;
        await SafeObserverInvoker.InvokeAsync<object>(new object(),
            _ => throw new OperationCanceledException("not from our token"),
            onFault: ex => captured = ex,
            cancellationToken: CancellationToken.None);
        Assert.IsType<OperationCanceledException>(captured);
    }

    [Fact]
    public async Task InvokeAsync_swallows_a_non_cancellation_fault_even_when_the_token_is_cancelled()
    {
        // Only OperationCanceledException is re-thrown on a cancelled token; other faults are swallowed.
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Exception? captured = null;
        await SafeObserverInvoker.InvokeAsync<object>(new object(),
            _ => throw new InvalidOperationException("boom"),
            onFault: ex => captured = ex,
            cancellationToken: cts.Token);
        Assert.IsType<InvalidOperationException>(captured);
    }

    [Fact]
    public async Task InvokeAsync_swallowed_fault_does_not_invoke_onFault_for_propagated_cancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var faultReported = false;
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            SafeObserverInvoker.InvokeAsync<object>(new object(),
                _ => throw new OperationCanceledException(cts.Token),
                onFault: _ => faultReported = true,
                cancellationToken: cts.Token));

        // Cooperative shutdown propagates; it is not downgraded to a reported fault.
        Assert.False(faultReported);
    }

    [Fact]
    public async Task InvokeAsync_happy_path_does_not_invoke_onFault()
    {
        var faultReported = false;
        await SafeObserverInvoker.InvokeAsync(new object(),
            _ => Task.CompletedTask,
            onFault: _ => faultReported = true);
        Assert.False(faultReported);
    }

    [Fact]
    public void Resolve_null_resolve_throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            SafeObserverInvoker.Resolve<object>(null!, _ => { }));
    }

    [Fact]
    public void Resolve_null_action_throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            SafeObserverInvoker.Resolve<object>(() => new object(), null!));
    }

    [Fact]
    public void Resolve_happy_path_runs_the_action_with_the_resolved_observer()
    {
        var observer = new object();
        object? received = null;
        SafeObserverInvoker.Resolve(() => observer, o => received = o);
        Assert.Same(observer, received);
    }

    [Fact]
    public void Resolve_swallows_a_fault_thrown_by_the_action_itself()
    {
        Exception? captured = null;
        SafeObserverInvoker.Resolve(() => new object(),
            _ => throw new InvalidOperationException("action threw"),
            onFault: ex => captured = ex);
        Assert.IsType<InvalidOperationException>(captured);
    }

    [Fact]
    public void Resolve_null_factory_result_does_not_invoke_onFault()
    {
        // A null observer is a no-op, not a fault.
        var faultReported = false;
        SafeObserverInvoker.Resolve<object>(() => null, _ => { }, onFault: _ => faultReported = true);
        Assert.False(faultReported);
    }
}
