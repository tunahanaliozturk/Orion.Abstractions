<p align="center">
  <img src="docs/logo.png" alt="Orion.Abstractions" width="150" />
</p>

# Orion.Abstractions

[![CI/CD](https://github.com/tunahanaliozturk/Orion.Abstractions/actions/workflows/ci-cd.yml/badge.svg)](https://github.com/tunahanaliozturk/Orion.Abstractions/actions/workflows/ci-cd.yml)
[![NuGet](https://img.shields.io/nuget/v/Orion.Abstractions.svg)](https://www.nuget.org/packages/Orion.Abstractions/)

Shared foundation primitives for the **Orion** family of .NET libraries. Three primitives kept being re-implemented (and kept drifting) across the family: fault-safe observer invocation, OpenTelemetry instrumentation conventions, and a testable clock. They now live here, once, correctly. The package has no Orion dependencies of its own, so any library can depend on it to inherit the Orion conventions.

## Features

- **Fault-safe observer invocation** (`SafeObserverInvoker`) - a null observer is a no-op, observer faults are swallowed so an observability outage cannot break the load-bearing path, and `OperationCanceledException` always propagates on cancellation. Includes a resolve-inside-the-guard variant so a throwing observer constructor cannot abort the host path at resolution time.
- **OpenTelemetry conventions** (`OrionInstrumentation`) - a base class that pairs a consistently named `ActivitySource` and `Meter`, plus a static-tag stamping pattern for multi-tenant / multi-region dashboard splitting without a second `Meter`.
- **Instance-scoped instrumentation** (`OrionInstrumentation`) - an instance can opt into a per-instance scope id and extra Meter-level tags, so its `Meter` carries an `orion.instance` tag (plus any custom tags) for per-instance metric partitioning. `OrionInstrumentation.ListensTo` then filters a `MeterListener` to exactly one instance's instruments, even when several live instances share the same Meter name.
- **Testable clock** (`IOrionClock` / `SystemOrionClock`) - a thin seam over `TimeProvider` so every Orion background worker, lease, and scheduler shares one clock contract and one DI registration.
- **Deterministic test clock** (`FrozenOrionClock`, in `Orion.Abstractions.Testing`) - a frozen, advanceable clock for testing lease expiry, grace periods, and scheduled work without real delays.
- **One-line DI registration** (`AddOrionAbstractions`) - registers the production clock via `TryAdd`, so it is safe to call from multiple Orion packages and a consumer override always wins.
- No dependencies beyond `Microsoft.Extensions.DependencyInjection.Abstractions`; multi-targets `net8.0`, `net9.0`, and `net10.0`; nullable enabled and warnings-as-errors.

## Install

```bash
dotnet add package Orion.Abstractions

# Optional: the testing companion (FrozenOrionClock), reference from your test project only
dotnet add package Orion.Abstractions.Testing
```

## Quick start

```csharp
using Microsoft.Extensions.DependencyInjection;
using Moongazing.Orion.Abstractions;
using Moongazing.Orion.Abstractions.Time;

var services = new ServiceCollection();
services.AddOrionAbstractions(); // registers IOrionClock -> SystemOrionClock (TryAdd)

using var provider = services.BuildServiceProvider();
var clock = provider.GetRequiredService<IOrionClock>();

DateTimeOffset now = clock.UtcNow;
long start = clock.GetTimestamp();
// ... do work ...
TimeSpan elapsed = clock.GetElapsedTime(start);
```

## Usage

### Fault-safe observer invocation

Route every consumer-supplied observer hook through `SafeObserverInvoker`. A null observer is skipped, a faulting observer is swallowed (and optionally reported), and cancellation is never downgraded to a swallowed warning.

```csharp
using Moongazing.Orion.Abstractions.Observers;

// Synchronous: a null observer is a no-op; a fault is swallowed and reported.
SafeObserverInvoker.Invoke(observer, o => o.OnSomething(payload),
    onFault: ex => logger.LogWarning(ex, "observer faulted; host continued"));

// Asynchronous: OperationCanceledException propagates when the token is cancelled.
await SafeObserverInvoker.InvokeAsync(observer,
    o => o.OnSomethingAsync(payload),
    onFault: ex => logger.LogWarning(ex, "observer faulted"),
    cancellationToken: ct);

// Resolution itself inside the guard: a throwing observer ctor cannot abort the host path.
SafeObserverInvoker.Resolve(
    () => serviceProvider.GetService<IMyObserver>(),
    o => o.OnSomething(payload),
    onFault: ex => logger.LogWarning(ex, "observer resolution faulted"));
```

### OpenTelemetry instrumentation

Derive a sealed diagnostics class from `OrionInstrumentation`. It exposes one `ActivitySource` and one `Meter` sharing a name and version. Create your instruments on `Meter`, and stamp every measurement through `Tag(...)` so the configured static tags are appended.

```csharp
using System.Diagnostics.Metrics;
using Moongazing.Orion.Abstractions.Diagnostics;

public sealed class MyDiagnostics : OrionInstrumentation
{
    public MyDiagnostics() : base("Moongazing.MyPackage", "1.0.0")
    {
        Things = Meter.CreateCounter<long>("my.things");
    }

    public Counter<long> Things { get; }
}

var diag = new MyDiagnostics();

// Set once at startup (single-threaded). These tags stamp every later measurement.
diag.SetStaticTags(new Dictionary<string, string> { ["tenant"] = tenantId });

// Tag(...) appends the static tags to the per-measurement tag.
diag.Things.Add(1, diag.Tag(new("outcome", "ok")));
```

When no static tags are configured, `Tag(...)` short-circuits to a single-element array, so the common single-tenant path stays allocation-light.

### Instance-scoped instrumentation

When a single process holds several instances that share one Meter name (or tests run them in parallel), a name-filtered `MeterListener` cannot tell them apart and double-counts. Pass an `instanceScopeId` (and optionally extra `instanceTags`) to the scoped base constructor: the `Meter` is then created with the `orion.instance` tag (`OrionInstrumentation.InstanceTagKey`) and a non-null `Meter.Scope`, so a collector can split metrics per instance. The default name/version constructor leaves the Meter unscoped, matching prior behavior.

```csharp
using System.Diagnostics.Metrics;
using Moongazing.Orion.Abstractions.Diagnostics;

public sealed class WorkerDiagnostics : OrionInstrumentation
{
    public WorkerDiagnostics(string instanceScopeId)
        : base("Moongazing.MyPackage", "1.0.0", instanceScopeId)
    {
        JobsProcessed = Meter.CreateCounter<long>("jobs.processed");
    }

    public Counter<long> JobsProcessed { get; }
}

using var first = new WorkerDiagnostics("worker-1");
using var second = new WorkerDiagnostics("worker-2");

// first.InstanceScopeId == "worker-1"; the Meter carries orion.instance=worker-1.
```

`ListensTo` filters a `MeterListener` to exactly one instance's instruments by Meter reference identity, so it is robust even if two instances are configured with the same `instanceScopeId`:

```csharp
using var listener = new MeterListener();
listener.InstrumentPublished = (instrument, l) =>
{
    if (OrionInstrumentation.ListensTo(instrument, first))
    {
        l.EnableMeasurementEvents(instrument); // only first's instruments are enabled
    }
};
listener.Start();
```

The reserved `orion.instance` key cannot be supplied in `instanceTags`; doing so throws `ArgumentException`. Custom `instanceTags` are merged alongside it and stamp the Meter itself, independent of the per-measurement `StaticTags`.

### Testable clock

Depend on `IOrionClock` instead of `DateTime.UtcNow` or `Stopwatch`. Production binds `SystemOrionClock` (over `TimeProvider.System`); tests bind `FrozenOrionClock`.

```csharp
using Moongazing.Orion.Abstractions.Testing;

var clock = new FrozenOrionClock(); // starts frozen at 2026-01-01Z by default
long start = clock.GetTimestamp();

clock.Advance(TimeSpan.FromSeconds(31)); // drive a lease past expiry, no real delay

Assert.Equal(TimeSpan.FromSeconds(31), clock.GetElapsedTime(start));
```

`Advance` moves both the wall clock and the monotonic timestamp; `SetUtcNow` moves only the wall clock. Both reject going backward, matching a real monotonic clock.

## Configuration

`AddOrionAbstractions()` uses `TryAddSingleton`, so the first registration wins. To supply your own clock (for example, a `SystemOrionClock` over a custom `TimeProvider`), register it before calling `AddOrionAbstractions`:

```csharp
services.AddSingleton<IOrionClock>(new SystemOrionClock(myTimeProvider));
services.AddOrionAbstractions(); // TryAdd no-ops because a clock is already registered
```

## Telemetry / Diagnostics

`OrionInstrumentation` is the integration point for OpenTelemetry. The `ActivitySource` and `Meter` are named with the value you pass to the base constructor, so wire them into your OpenTelemetry pipeline by that name:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource("Moongazing.MyPackage"))
    .WithMetrics(m => m.AddMeter("Moongazing.MyPackage"));
```

The static-tag pattern lets you split dashboards by tenant, region, or environment without standing up a second `Meter`. Tags configured after the host starts emitting do not retroactively apply to already-emitted measurements, which is why `SetStaticTags` is intended to be called once at startup.

## Testing

- Reference `Orion.Abstractions.Testing` from test projects and inject `FrozenOrionClock` wherever production injects `IOrionClock`. Advancing the clock makes lease-expiry, grace-period, and scheduler tests deterministic and instant.
- `SafeObserverInvoker` is static and side-effect-free apart from the callbacks you pass, so it is straightforward to assert the no-op, happy, fault-swallowing, and cancellation-propagating paths directly.

A micro-benchmark suite (BenchmarkDotNet) covers the allocation- and CPU-bearing surface: tag stamping, observer dispatch, and the clock seam. See [benchmarks.md](benchmarks.md). No measured numbers are committed; run the suite locally to produce them for your hardware.

## Packages

| Package                      | Purpose                                        |
| ---------------------------- | ---------------------------------------------- |
| `Orion.Abstractions`         | The shared primitives above.                   |
| `Orion.Abstractions.Testing` | `FrozenOrionClock` and future test doubles.    |

## Versioning

Follows [Semantic Versioning](https://semver.org/). The library multi-targets `net8.0`, `net9.0`, and `net10.0`. (The benchmark host runs on net8.0 and net9.0 only, because BenchmarkDotNet 0.14.0 has no .NET 10 job moniker.)

## Documentation

- [docs/FEATURES.md](docs/FEATURES.md) - a deeper breakdown of each feature and its public types and methods.
- [docs/ROADMAP.md](docs/ROADMAP.md) - ideas under consideration.
- [benchmarks.md](benchmarks.md) - what the benchmark suite measures and how to run it.

## Contributing

Contributions are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md) and the [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).

## License

[MIT](LICENSE).
