namespace Moongazing.Orion.Abstractions.Tests;

using Moongazing.Orion.Abstractions.Observers;
using Moongazing.Orion.Abstractions.Testing;
using Xunit;

public sealed class RecordingObserverTests
{
    private interface IThing
    {
        void Do();
    }

    private sealed class Thing : IThing
    {
        public int Calls { get; private set; }

        public void Do() => Calls++;
    }

    [Fact]
    public void Track_records_each_completed_invocation_with_the_observer_instance()
    {
        var thing = new Thing();
        var recorder = new RecordingObserver<IThing>(thing);

        SafeObserverInvoker.Invoke(recorder.Observer, recorder.Track(t => t.Do()), recorder.OnFault);

        Assert.True(recorder.WasInvoked);
        Assert.Equal(1, recorder.InvocationCount);
        Assert.Same(thing, Assert.Single(recorder.Invocations));
        Assert.Equal(1, thing.Calls);
        Assert.False(recorder.Faulted);
    }

    [Fact]
    public void Track_works_without_an_inner_action()
    {
        var thing = new Thing();
        var recorder = new RecordingObserver<IThing>(thing);

        SafeObserverInvoker.Invoke(recorder.Observer, recorder.Track());

        Assert.Equal(1, recorder.InvocationCount);
        Assert.Equal(0, thing.Calls); // no inner action ran, but the invocation was still recorded
    }

    [Fact]
    public void Null_observer_records_no_invocation_and_no_fault()
    {
        var recorder = new RecordingObserver<IThing>(observer: null);

        SafeObserverInvoker.Invoke(recorder.Observer, recorder.Track(t => t.Do()), recorder.OnFault);

        Assert.False(recorder.WasInvoked);
        Assert.Empty(recorder.Invocations);
        Assert.False(recorder.Faulted);
    }

    [Fact]
    public void Parameterless_ctor_drives_the_null_observer_path()
    {
        var recorder = new RecordingObserver<IThing>();

        Assert.Null(recorder.Observer);
        SafeObserverInvoker.Invoke(recorder.Observer, recorder.Track(t => t.Do()), recorder.OnFault);

        Assert.Equal(0, recorder.InvocationCount);
    }

    [Fact]
    public void A_faulting_action_records_the_fault_and_not_an_invocation()
    {
        var recorder = new RecordingObserver<object>(new object());

        SafeObserverInvoker.Invoke(
            recorder.Observer,
            recorder.Track(_ => throw new InvalidOperationException("boom")),
            recorder.OnFault);

        Assert.False(recorder.WasInvoked);
        Assert.True(recorder.Faulted);
        var fault = recorder.SingleFault();
        Assert.IsType<InvalidOperationException>(fault);
        Assert.Equal("boom", fault.Message);
    }

    [Fact]
    public void SingleFault_throws_when_no_fault_recorded()
    {
        var recorder = new RecordingObserver<object>(new object());
        Assert.Throws<InvalidOperationException>(() => recorder.SingleFault());
    }

    [Fact]
    public void SingleFault_throws_when_more_than_one_fault_recorded()
    {
        var recorder = new RecordingObserver<object>(new object());

        SafeObserverInvoker.Invoke(recorder.Observer, recorder.Track(_ => throw new InvalidOperationException()), recorder.OnFault);
        SafeObserverInvoker.Invoke(recorder.Observer, recorder.Track(_ => throw new InvalidOperationException()), recorder.OnFault);

        Assert.Equal(2, recorder.FaultCount);
        Assert.Throws<InvalidOperationException>(() => recorder.SingleFault());
    }

    [Fact]
    public void Invocations_are_recorded_in_order_across_multiple_calls()
    {
        var first = new Thing();
        var second = new Thing();
        var recorder = new RecordingObserver<IThing>(first);

        SafeObserverInvoker.Invoke(first, recorder.Track(t => t.Do()), recorder.OnFault);
        SafeObserverInvoker.Invoke(second, recorder.Track(t => t.Do()), recorder.OnFault);

        Assert.Equal(2, recorder.InvocationCount);
        Assert.Same(first, recorder.Invocations[0]);
        Assert.Same(second, recorder.Invocations[1]);
    }

    [Fact]
    public async Task TrackAsync_records_a_completed_async_invocation()
    {
        var thing = new Thing();
        var recorder = new RecordingObserver<IThing>(thing);

        await SafeObserverInvoker.InvokeAsync(
            recorder.Observer,
            recorder.TrackAsync(async t =>
            {
                await Task.Yield();
                t.Do();
            }),
            recorder.OnFault);

        Assert.Equal(1, recorder.InvocationCount);
        Assert.Same(thing, Assert.Single(recorder.Invocations));
        Assert.Equal(1, thing.Calls);
        Assert.False(recorder.Faulted);
    }

    [Fact]
    public async Task TrackAsync_records_a_swallowed_async_fault_but_not_an_invocation()
    {
        var recorder = new RecordingObserver<object>(new object());

        await SafeObserverInvoker.InvokeAsync(
            recorder.Observer,
            recorder.TrackAsync(_ => throw new InvalidOperationException("async boom")),
            recorder.OnFault);

        Assert.False(recorder.WasInvoked);
        Assert.IsType<InvalidOperationException>(recorder.SingleFault());
    }

    [Fact]
    public async Task TrackAsync_does_not_record_a_propagated_cancellation_as_a_fault()
    {
        var recorder = new RecordingObserver<object>(new object());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            SafeObserverInvoker.InvokeAsync(
                recorder.Observer,
                recorder.TrackAsync(_ => throw new OperationCanceledException(cts.Token)),
                recorder.OnFault,
                cts.Token));

        // Cooperative shutdown propagates; it is not downgraded to a recorded observer fault,
        // and no invocation completed.
        Assert.False(recorder.Faulted);
        Assert.False(recorder.WasInvoked);
    }

    [Fact]
    public void Composes_with_Resolve_recording_a_resolved_invocation()
    {
        var thing = new Thing();
        var recorder = new RecordingObserver<IThing>(thing);

        SafeObserverInvoker.Resolve(() => recorder.Observer, recorder.Track(t => t.Do()), recorder.OnFault);

        Assert.Equal(1, recorder.InvocationCount);
        Assert.Same(thing, Assert.Single(recorder.Invocations));
    }

    [Fact]
    public void Composes_with_Resolve_recording_a_throwing_factory_as_a_fault()
    {
        var recorder = new RecordingObserver<IThing>(new Thing());

        SafeObserverInvoker.Resolve<IThing>(
            () => throw new InvalidOperationException("ctor threw"),
            recorder.Track(t => t.Do()),
            recorder.OnFault);

        Assert.False(recorder.WasInvoked);
        Assert.IsType<InvalidOperationException>(recorder.SingleFault());
    }

    [Fact]
    public void Reset_clears_invocations_and_faults()
    {
        var recorder = new RecordingObserver<object>(new object());

        SafeObserverInvoker.Invoke(recorder.Observer, recorder.Track(), recorder.OnFault);
        SafeObserverInvoker.Invoke(recorder.Observer, recorder.Track(_ => throw new InvalidOperationException()), recorder.OnFault);
        Assert.True(recorder.WasInvoked);
        Assert.True(recorder.Faulted);

        recorder.Reset();

        Assert.False(recorder.WasInvoked);
        Assert.False(recorder.Faulted);
        Assert.Empty(recorder.Invocations);
        Assert.Empty(recorder.Faults);
    }

    [Fact]
    public void Invocations_snapshot_is_independent_of_later_calls()
    {
        var recorder = new RecordingObserver<object>(new object());

        SafeObserverInvoker.Invoke(recorder.Observer, recorder.Track(), recorder.OnFault);
        var snapshot = recorder.Invocations;
        SafeObserverInvoker.Invoke(recorder.Observer, recorder.Track(), recorder.OnFault);

        // The snapshot taken after the first call is not mutated by the second.
        Assert.Single(snapshot);
        Assert.Equal(2, recorder.InvocationCount);
    }
}
