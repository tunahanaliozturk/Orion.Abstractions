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
}
