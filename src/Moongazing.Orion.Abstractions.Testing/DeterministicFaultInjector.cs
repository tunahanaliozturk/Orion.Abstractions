namespace Moongazing.Orion.Abstractions.Testing;

using System;
using System.Threading;
using System.Threading.Tasks;

using Moongazing.Orion.Abstractions.Time;

/// <summary>
/// A deterministic, reproducible fault injector for reliability tests (retry, backoff, exactly-once,
/// circuit-breaking). There is no randomness: given the same schedule it fails on exactly the same
/// attempts, so the test never flakes and a failure is always reproducible. Attempt counting is
/// thread-safe.
/// <para>
/// Compose it into the operation under test by calling <see cref="Next"/> (or wrapping the operation
/// with <see cref="Run{T}(Func{T})"/> / <see cref="RunAsync{T}(Func{Task{T}}, CancellationToken)"/>)
/// at the start of each attempt: it throws the configured fault when this attempt is scheduled to
/// fail, and otherwise lets the attempt proceed. Time-based schedules read an
/// <see cref="IOrionClock"/> (pair it with <see cref="FrozenOrionClock"/>) so a test drives recovery
/// by advancing the clock rather than sleeping.
/// </para>
/// </summary>
public sealed class DeterministicFaultInjector
{
    private readonly Func<int, bool> shouldFail;
    private readonly Func<Exception> faultFactory;
    private int attempts;

    private DeterministicFaultInjector(Func<int, bool> shouldFail, Func<Exception>? faultFactory)
    {
        this.shouldFail = shouldFail;
        this.faultFactory = faultFactory ?? (static () => new DeterministicFaultException());
    }

    /// <summary>The number of attempts recorded so far (each <see cref="Next"/> / <c>Run</c> counts one).</summary>
    public int Attempts => Volatile.Read(ref attempts);

    /// <summary>
    /// Fail the first <paramref name="count"/> attempts, then succeed forever after — the canonical
    /// "transient failure that eventually recovers" shape for a retry test.
    /// </summary>
    /// <param name="count">How many leading attempts fail. Zero means never fail.</param>
    /// <param name="fault">Optional factory for the exception thrown; defaults to <see cref="DeterministicFaultException"/>.</param>
    public static DeterministicFaultInjector FailFirst(int count, Func<Exception>? fault = null)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Fail count cannot be negative.");
        }
        return new DeterministicFaultInjector(attempt => attempt <= count, fault);
    }

    /// <summary>Fail on exactly the given 1-based attempt numbers, and succeed on all others.</summary>
    /// <param name="attempts">The 1-based attempt numbers that should fail.</param>
    public static DeterministicFaultInjector FailOnAttempts(params int[] attempts)
        => FailOnAttempts(attempts, fault: null);

    /// <summary>Fail on exactly the given 1-based attempt numbers, throwing <paramref name="fault"/>.</summary>
    /// <param name="attempts">The 1-based attempt numbers that should fail.</param>
    /// <param name="fault">Optional factory for the exception thrown.</param>
    public static DeterministicFaultInjector FailOnAttempts(int[] attempts, Func<Exception>? fault)
    {
        ArgumentNullException.ThrowIfNull(attempts);
        foreach (var attempt in attempts)
        {
            if (attempt < 1)
            {
                // Attempts are 1-based (Next() yields 1, 2, ...), so a 0 or negative number could
                // never match. Reject it rather than silently producing an injector that never
                // faults - which would turn a typo into a passing reliability test.
                throw new ArgumentOutOfRangeException(nameof(attempts), attempt, "Attempt numbers are 1-based; each must be >= 1.");
            }
        }
        var set = new HashSet<int>(attempts);
        return new DeterministicFaultInjector(set.Contains, fault);
    }

    /// <summary>Fail every attempt — models a dependency that is down for the whole test.</summary>
    /// <param name="fault">Optional factory for the exception thrown.</param>
    public static DeterministicFaultInjector AlwaysFail(Func<Exception>? fault = null)
        => new(static _ => true, fault);

    /// <summary>Never fail — a no-op baseline useful for asserting the happy path counts attempts.</summary>
    public static DeterministicFaultInjector NeverFail()
        => new(static _ => false, null);

    /// <summary>
    /// Fail while the clock reads before <paramref name="recoversAt"/>, then succeed from that instant
    /// on. Pair with <see cref="FrozenOrionClock"/> and advance it to model a dependency that recovers
    /// at a known time, independent of the attempt count.
    /// </summary>
    /// <param name="clock">The clock consulted on each attempt.</param>
    /// <param name="recoversAt">The instant at (and after) which attempts succeed.</param>
    /// <param name="fault">Optional factory for the exception thrown.</param>
    public static DeterministicFaultInjector FailUntil(IOrionClock clock, DateTimeOffset recoversAt, Func<Exception>? fault = null)
    {
        ArgumentNullException.ThrowIfNull(clock);
        return new DeterministicFaultInjector(_ => clock.UtcNow < recoversAt, fault);
    }

    /// <summary>A custom deterministic schedule: fail an attempt when <paramref name="shouldFail"/> returns true for its 1-based number.</summary>
    /// <param name="shouldFail">Predicate over the 1-based attempt number. Must be deterministic to keep the test reproducible.</param>
    /// <param name="fault">Optional factory for the exception thrown.</param>
    public static DeterministicFaultInjector When(Func<int, bool> shouldFail, Func<Exception>? fault = null)
    {
        ArgumentNullException.ThrowIfNull(shouldFail);
        return new DeterministicFaultInjector(shouldFail, fault);
    }

    /// <summary>
    /// Record one attempt and throw the configured fault if this attempt is scheduled to fail. Call
    /// it at the start of each guarded operation.
    /// </summary>
    /// <exception cref="Exception">The configured fault (default <see cref="DeterministicFaultException"/>) when this attempt fails.</exception>
    public void Next()
    {
        var attempt = Interlocked.Increment(ref attempts);
        if (shouldFail(attempt))
        {
            throw faultFactory();
        }
    }

    /// <summary>Run <paramref name="operation"/> as one guarded attempt: fault first if scheduled, otherwise invoke and return it.</summary>
    /// <typeparam name="T">The operation's result type.</typeparam>
    /// <param name="operation">The operation to run when this attempt is not scheduled to fail.</param>
    /// <returns>The operation's result.</returns>
    public T Run<T>(Func<T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        Next();
        return operation();
    }

    /// <summary>Run <paramref name="operation"/> as one guarded attempt, faulting first if scheduled.</summary>
    /// <param name="operation">The operation to run when this attempt is not scheduled to fail.</param>
    public void Run(Action operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        Next();
        operation();
    }

    /// <summary>Asynchronously run <paramref name="operation"/> as one guarded attempt, faulting first if scheduled.</summary>
    /// <typeparam name="T">The operation's result type.</typeparam>
    /// <param name="operation">The async operation to run when this attempt is not scheduled to fail.</param>
    /// <param name="cancellationToken">Observed before the attempt is counted.</param>
    /// <returns>The operation's result.</returns>
    public async Task<T> RunAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();
        Next();
        return await operation().ConfigureAwait(false);
    }
}
