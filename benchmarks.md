# Benchmarks

Micro-benchmarks for the allocation- and CPU-bearing public surface of
**Orion.Abstractions**, built with [BenchmarkDotNet](https://benchmarkdotnet.org/) 0.14.0.

This is an abstractions package, so most of its public API is interfaces and base types with
no measurable logic. The benchmarks target the three places where real per-call work happens:
tag stamping on the instrumentation base, the fault-safe observer dispatch wrapper, and the
clock seam. There is deliberately nothing here that touches a database, network, or any
external service.

## Project

`benchmarks/Moongazing.Orion.Abstractions.Benchmarks` references the main
`Moongazing.Orion.Abstractions` project directly and exercises only its real public API.

Each benchmark class runs under two runtimes, .NET 8 and .NET 9
(`[SimpleJob(RuntimeMoniker.Net80)]` and `[SimpleJob(RuntimeMoniker.Net90)]`), with
`[MemoryDiagnoser]` enabled so allocations are reported alongside timing. BenchmarkDotNet
0.14.0 has no .NET 10 moniker, so the benchmark host runs on those two even though the
library itself also multi-targets net10.0.

## What is measured

### `InstrumentationBenchmarks` — `OrionInstrumentation` tag stamping

`OrionInstrumentation.Tag(...)` runs on every metric emission in every Orion package, so its
allocation profile (one array per call) is the load-bearing cost.

- `Tag_NoStaticTags` (baseline) — the single-tenant fast path, where no static tags are
  configured and `Tag` short-circuits to a single-element array.
- `Tag_ThreeStaticTags` — a tenant/region/env split: allocate, `Array.Copy` the static tags,
  append the per-measurement tag.
- `SetStaticTags_Three` — the startup-time snapshot rebuild: allocate the array, iterate the
  dictionary, project each pair. Reuses one instrumentation instance so only the snapshot
  build is measured.

### `SafeObserverInvokerBenchmarks` — fault-safe observer dispatch

`SafeObserverInvoker` wraps every consumer observer hook across the family, so the null-check
/ try-catch / delegate-invocation overhead sits on the load-bearing path.

- `Invoke_NullObserver` (baseline) — null observer, the call site is skipped (the dominant
  runtime case).
- `Invoke_Observer` — a non-null observer invoked through the fault guard.
- `Invoke_FaultingObserver` — a throwing observer whose exception is caught and routed to the
  `onFault` sink.
- `Resolve_Observer` — the resolve-then-invoke variant with the factory call inside the guard.

### `OrionClockBenchmarks` — `SystemOrionClock` over `TimeProvider`

Leases, schedulers, and background workers read the clock frequently. These measure the thin
seam's overhead versus calling `TimeProvider` directly, invoked through the `IOrionClock`
interface to include the virtual dispatch every consumer pays.

- `UtcNow` — current UTC instant.
- `GetTimestamp` — monotonic timestamp.
- `GetElapsedTime` — elapsed time from a prior timestamp.

No measured numbers are committed here; run the suite locally to produce them for your
hardware and runtime.

## Running

From the repository root:

```bash
# Run everything
dotnet run -c Release --project benchmarks/Moongazing.Orion.Abstractions.Benchmarks

# Filter to one class
dotnet run -c Release --project benchmarks/Moongazing.Orion.Abstractions.Benchmarks -- --filter "*InstrumentationBenchmarks*"

# List the available benchmarks
dotnet run -c Release --project benchmarks/Moongazing.Orion.Abstractions.Benchmarks -- --list flat
```

Benchmarks must be run in `Release`. BenchmarkDotNet writes its reports (Markdown, HTML, CSV)
to `BenchmarkDotNet.Artifacts/` in the working directory.
