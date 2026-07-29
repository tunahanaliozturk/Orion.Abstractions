namespace Moongazing.Orion.Abstractions.Tests;

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

using Moongazing.Orion.Abstractions.Testing;

using Xunit;

/// <summary>
/// Coverage for <see cref="DeterministicFaultInjector"/>: each schedule fails on exactly the
/// documented attempts, faults are the configured type, the guarded <c>Run</c> helpers gate the
/// operation, and the whole thing is reproducible and thread-safe.
/// </summary>
public sealed class DeterministicFaultInjectorTests
{
    [Fact]
    public void FailFirst_fails_the_leading_attempts_then_succeeds()
    {
        var injector = DeterministicFaultInjector.FailFirst(2);

        Assert.Throws<DeterministicFaultException>(injector.Next);
        Assert.Throws<DeterministicFaultException>(injector.Next);
        injector.Next(); // third attempt succeeds
        injector.Next();

        Assert.Equal(4, injector.Attempts);
    }

    [Fact]
    public void FailFirst_zero_never_fails()
    {
        var injector = DeterministicFaultInjector.FailFirst(0);

        injector.Next();
        injector.Next();

        Assert.Equal(2, injector.Attempts);
    }

    [Fact]
    public void FailFirst_rejects_a_negative_count()
        => Assert.Throws<ArgumentOutOfRangeException>(() => DeterministicFaultInjector.FailFirst(-1));

    [Fact]
    public void FailOnAttempts_fails_exactly_the_listed_attempts()
    {
        var injector = DeterministicFaultInjector.FailOnAttempts(2, 4);

        injector.Next();                                                // 1 ok
        Assert.Throws<DeterministicFaultException>(injector.Next);       // 2 fail
        injector.Next();                                                // 3 ok
        Assert.Throws<DeterministicFaultException>(injector.Next);       // 4 fail
        injector.Next();                                                // 5 ok

        Assert.Equal(5, injector.Attempts);
    }

    [Fact]
    public void AlwaysFail_fails_every_attempt()
    {
        var injector = DeterministicFaultInjector.AlwaysFail();

        Assert.Throws<DeterministicFaultException>(injector.Next);
        Assert.Throws<DeterministicFaultException>(injector.Next);
    }

    [Fact]
    public void NeverFail_counts_attempts_without_faulting()
    {
        var injector = DeterministicFaultInjector.NeverFail();

        injector.Next();
        injector.Next();
        injector.Next();

        Assert.Equal(3, injector.Attempts);
    }

    [Fact]
    public void FailUntil_recovers_when_the_clock_reaches_the_instant()
    {
        var clock = new FrozenOrionClock();
        var recoversAt = clock.UtcNow.AddSeconds(30);
        var injector = DeterministicFaultInjector.FailUntil(clock, recoversAt);

        Assert.Throws<DeterministicFaultException>(injector.Next); // before recovery
        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Throws<DeterministicFaultException>(injector.Next); // still before
        clock.Advance(TimeSpan.FromSeconds(20));                   // now == recoversAt
        injector.Next();                                           // recovered
        injector.Next();

        Assert.Equal(4, injector.Attempts);
    }

    [Fact]
    public void When_uses_the_custom_deterministic_predicate()
    {
        var injector = DeterministicFaultInjector.When(attempt => attempt % 2 == 0);

        injector.Next();                                          // 1 ok
        Assert.Throws<DeterministicFaultException>(injector.Next); // 2 fail
        injector.Next();                                          // 3 ok
        Assert.Throws<DeterministicFaultException>(injector.Next); // 4 fail
    }

    [Fact]
    public void A_custom_fault_factory_throws_the_configured_exception()
    {
        var injector = DeterministicFaultInjector.AlwaysFail(() => new InvalidOperationException("boom"));

        var ex = Assert.Throws<InvalidOperationException>(injector.Next);
        Assert.Equal("boom", ex.Message);
    }

    [Fact]
    public void Run_returns_the_operation_result_on_a_passing_attempt()
    {
        var injector = DeterministicFaultInjector.FailFirst(1);

        Assert.Throws<DeterministicFaultException>(() => injector.Run(() => 42)); // 1 fails, operation not run
        var value = injector.Run(() => 42);                                       // 2 passes
        Assert.Equal(42, value);
    }

    [Fact]
    public void Run_does_not_invoke_the_operation_on_a_failing_attempt()
    {
        var injector = DeterministicFaultInjector.AlwaysFail();
        var ran = false;

        Assert.Throws<DeterministicFaultException>(() => injector.Run(() => ran = true));

        Assert.False(ran);
    }

    [Fact]
    public async Task RunAsync_gates_the_async_operation()
    {
        var injector = DeterministicFaultInjector.FailFirst(1);

        await Assert.ThrowsAsync<DeterministicFaultException>(() => injector.RunAsync(() => Task.FromResult(7)));
        var value = await injector.RunAsync(() => Task.FromResult(7));

        Assert.Equal(7, value);
    }

    [Fact]
    public void The_same_schedule_is_reproducible()
    {
        static bool[] Outcomes(DeterministicFaultInjector injector)
            => Enumerable.Range(0, 6).Select(_ =>
            {
                try { injector.Next(); return true; }
                catch (DeterministicFaultException) { return false; }
            }).ToArray();

        var a = Outcomes(DeterministicFaultInjector.FailOnAttempts(1, 3, 5));
        var b = Outcomes(DeterministicFaultInjector.FailOnAttempts(1, 3, 5));

        Assert.Equal(a, b);
        bool[] expected = [false, true, false, true, false, true];
        Assert.Equal(expected, a);
    }

    [Fact]
    public async Task Attempt_counting_is_thread_safe()
    {
        var injector = DeterministicFaultInjector.NeverFail();

        await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => Task.Run(injector.Next)));

        Assert.Equal(100, injector.Attempts);
    }

    [Fact]
    public async Task Every_scheduled_failure_fires_exactly_once_under_concurrency()
    {
        // Fail attempts 1..50; run 100 attempts concurrently. Exactly 50 must fault, whichever
        // attempts happen to land in the first 50 slots - the count is what a chaos test relies on.
        var injector = DeterministicFaultInjector.FailFirst(50);
        var faults = new ConcurrentBag<bool>();

        await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => Task.Run(() =>
        {
            try { injector.Next(); faults.Add(false); }
            catch (DeterministicFaultException) { faults.Add(true); }
        })));

        Assert.Equal(100, injector.Attempts);
        Assert.Equal(50, faults.Count(f => f));
    }
}
