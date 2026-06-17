using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Moongazing.Orion.Abstractions.Observers;

namespace Moongazing.Orion.Abstractions.Benchmarks;

/// <summary>
/// Measures the fault-safe observer dispatch path in <see cref="SafeObserverInvoker"/>.
/// Every Orion package routes its observer hooks through this, so the overhead of the
/// null-check / try-catch / delegate invocation wrapper sits on the load-bearing path.
/// Covers the no-op (null observer), happy (non-null observer invoked), and faulting
/// (exception swallowed) cases, plus the resolve-inside-the-guard variant.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net90)]
public class SafeObserverInvokerBenchmarks
{
    private readonly CountingObserver observer = new();
    private readonly Action<CountingObserver> action = static o => o.OnEvent();
    private readonly Action<CountingObserver> faulting = static _ => throw new InvalidOperationException("boom");
    private readonly Action<Exception> onFault = static _ => { };

    /// <summary>Null observer: the call site is skipped entirely (the dominant runtime case).</summary>
    [Benchmark(Baseline = true)]
    public void Invoke_NullObserver() => SafeObserverInvoker.Invoke<CountingObserver>(null, action);

    /// <summary>Non-null observer invoked through the fault guard.</summary>
    [Benchmark]
    public void Invoke_Observer() => SafeObserverInvoker.Invoke(observer, action);

    /// <summary>Faulting observer: exception caught and routed to the onFault sink.</summary>
    [Benchmark]
    public void Invoke_FaultingObserver() => SafeObserverInvoker.Invoke(observer, faulting, onFault);

    /// <summary>Resolve-then-invoke with the factory call inside the guard.</summary>
    [Benchmark]
    public void Resolve_Observer() => SafeObserverInvoker.Resolve(() => observer, action);

    private sealed class CountingObserver
    {
        public long Count;

        public void OnEvent() => Count++;
    }
}
