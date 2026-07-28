<p align="center">
  <img src="docs/logo.png" alt="Orion.Abstractions" width="150" />
</p>

# Orion.Abstractions

[![CI/CD](https://github.com/tunahanaliozturk/Orion.Abstractions/actions/workflows/ci-cd.yml/badge.svg)](https://github.com/tunahanaliozturk/Orion.Abstractions/actions/workflows/ci-cd.yml)
[![NuGet](https://img.shields.io/nuget/v/Orion.Abstractions.svg)](https://www.nuget.org/packages/Orion.Abstractions/)

The frozen spine of the **Orion** family of .NET libraries. Five things kept being re-implemented (and kept drifting) across the family: fault-safe observer invocation, OpenTelemetry naming and instrumentation, a testable clock, the options/DI registration shape, and the error vocabulary. They now live here, once, correctly. The package has no Orion dependencies of its own, so any library can depend on it to inherit the Orion conventions.

**At 1.0 the surface is frozen.** Every contract below is source- and binary-compatible across the whole 1.x line, so a package can bind to it without fear of drifting under a sibling's upgrade. The rules that go with it are in [docs/CONVENTIONS.md](docs/CONVENTIONS.md) - normative for every package in the family.

## Features

- **Fault-safe observer invocation** (`SafeObserverInvoker`) - a null observer is a no-op, observer faults are swallowed so an observability outage cannot break the load-bearing path, and `OperationCanceledException` always propagates on cancellation. Includes a resolve-inside-the-guard variant so a throwing observer constructor cannot abort the host path at resolution time.
- **OpenTelemetry conventions** (`OrionInstrumentation`) - a base class that pairs a consistently named `ActivitySource` and `Meter`, plus a static-tag stamping pattern for multi-tenant / multi-region dashboard splitting without a second `Meter`.
- **Instance-scoped instrumentation** (`OrionInstrumentation`) - an instance can opt into a per-instance scope id and extra Meter-level tags, so its `Meter` carries an `orion.instance` tag (plus any custom tags) for per-instance metric partitioning. `OrionInstrumentation.ListensTo` then filters a `MeterListener` to exactly one instance's instruments, even when several live instances share the same Meter name.
- **Testable clock** (`IOrionClock` / `SystemOrionClock`) - a thin seam over `TimeProvider` so every Orion background worker, lease, and scheduler shares one clock contract and one DI registration.
- **Deterministic test clock** (`FrozenOrionClock`, in `Orion.Abstractions.Testing`) - a frozen, advanceable clock for testing lease expiry, grace periods, and scheduled work without real delays.
- **Options convention** (`OrionOptions` / `AddOrionOptions`) - one base type, one collecting validator, one failure-message format. An options type states its invariants once and a misconfigured host learns *all* of its mistakes in a single startup instead of one per restart.
- **OTel naming surface** (`OrionTelemetry`) - the frozen constants for scope names, metric names, tag keys, and outcome values, so a dashboard written against one package generalises to all of them and no package invents a magic string.
- **Error vocabulary** (`IOrionResult` / `OrionError` / `OrionErrorCodes`) - the shape for returning an expected outcome instead of throwing on the hot path, plus the canonical error codes the web tier maps to status codes without a per-package table.
- **One-line DI registration** (`AddOrionAbstractions`) - registers the production clock via `TryAdd`, so it is safe to call from multiple Orion packages and a consumer override always wins.
- Dependencies limited to `Microsoft.Extensions.DependencyInjection.Abstractions` and `Microsoft.Extensions.Options`; multi-targets `net8.0`, `net9.0`, and `net10.0`; NativeAOT- and trim-clean, verified by a native binary smoke test in CI; nullable enabled and warnings-as-errors.

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

What an observer may and may not do (no throwing from `onFault`, no blocking the host, cancellation semantics) is spelled out in the normative [observer contract](docs/observer-contract.md). The `RecordingObserver` test double below lets you assert your observers honor it.

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

### Options and validation

Derive your package's options from `OrionOptions`, state the invariants once, and register with `AddOrionOptions`. Validation collects every failure rather than throwing on the first, so one startup surfaces the whole misconfiguration.

```csharp
using Moongazing.Orion.Abstractions.Configuration;

public sealed class OrionLockOptions : OrionOptions
{
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan RenewalInterval { get; set; } = TimeSpan.FromSeconds(10);

    public override void Validate(OrionOptionsValidationContext context)
    {
        base.Validate(context);
        context.RequirePositive(LeaseDuration, nameof(LeaseDuration));
        context.Require(
            RenewalInterval < LeaseDuration,
            "RenewalInterval must be shorter than LeaseDuration, or a lease expires before it renews.");
    }
}

// In AddOrionLock():
services.AddOrionOptions<OrionLockOptions>(configure);
```

A rejected configuration fails resolution with one operator-facing message, in the same format for every package in the family:

```text
OrionLockOptions is invalid:
  - OrionLockOptions.LeaseDuration: must be greater than zero (was 00:00:00).
  - OrionLockOptions: RenewalInterval must be shorter than LeaseDuration, or a lease expires before it renews.
```

`AddOrionOptions` registers the validator with `TryAddEnumerable`, so calling `AddOrionX()` twice registers one validator - while both configuration delegates still apply. It returns the `OptionsBuilder<T>` so you can chain `ValidateOnStart()` where you reference the hosting package.

### Errors and results

Expected outcomes are results, not exceptions. `IOrionResult` is the shape; `OrionError` is the value; `OrionErrorCodes` is the canonical vocabulary. (The ergonomic result type with map/bind/match ships in the `OrionResult` package - the spine defines only the contract so cross-cutting code can read any Orion result without knowing which package produced it.)

```csharp
using Moongazing.Orion.Abstractions.Results;

public readonly struct LockResult : IOrionResult
{
    public bool IsSuccess { get; init; }
    public OrionError? Error { get; init; }

    public static LockResult Held(string key) => new()
    {
        Error = new OrionError(OrionErrorCodes.Conflict, "The lock is already held.", key),
    };
}
```

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

Build those names from `OrionTelemetry` rather than spelling them out, and take every shared tag key from `OrionTelemetry.Tags`:

```csharp
using Moongazing.Orion.Abstractions.Diagnostics;

internal sealed class LockInstrumentation()
    : OrionInstrumentation(OrionTelemetry.ScopeName("OrionLock"), "1.0.0")   // "Moongazing.OrionLock"
{
    public Histogram<double> AcquireDuration { get; } = Meter.CreateHistogram<double>(
        OrionTelemetry.MetricName("lock", "acquire.duration"), "ms");        // "orion.lock.acquire.duration"
}

instrumentation.AcquireDuration.Record(
    elapsed.TotalMilliseconds,
    instrumentation.Tag(new(OrionTelemetry.Tags.Outcome, OrionTelemetry.Outcomes.Success)));
```

Two naming schemes, deliberately: instrumentation scopes use the assembly-style `Moongazing.OrionLock` so they match the package names an operator enables in their OTel config, while metrics and tags use the lowercase dotted form the OpenTelemetry semantic conventions mandate. `orion.outcome` is a bounded, low-cardinality dimension - distinguish failures with `orion.error.code`, never by inventing a new outcome.

The static-tag pattern lets you split dashboards by tenant, region, or environment without standing up a second `Meter`. Tags configured after the host starts emitting do not retroactively apply to already-emitted measurements, which is why `SetStaticTags` is intended to be called once at startup.

## Testing

- Reference `Orion.Abstractions.Testing` from test projects and inject `FrozenOrionClock` wherever production injects `IOrionClock`. Advancing the clock makes lease-expiry, grace-period, and scheduler tests deterministic and instant.
- `SafeObserverInvoker` is static and side-effect-free apart from the callbacks you pass, so it is straightforward to assert the no-op, happy, fault-swallowing, and cancellation-propagating paths directly.
- `RecordingObserver<TObserver>` (also in `Orion.Abstractions.Testing`) records every observer invocation and every swallowed fault at a `SafeObserverInvoker` call site. Pass its `Track` / `TrackAsync` wrapper as the action and its `OnFault` as the fault hook, then assert your observers behave per the [observer contract](docs/observer-contract.md).

```csharp
using Moongazing.Orion.Abstractions.Observers;
using Moongazing.Orion.Abstractions.Testing;

var recorder = new RecordingObserver<IMyObserver>(myObserver);

SafeObserverInvoker.Invoke(recorder.Observer, recorder.Track(o => o.OnSomething(payload)), recorder.OnFault);

Assert.True(recorder.WasInvoked); // the action ran to completion
Assert.False(recorder.Faulted);   // no fault was swallowed
```

A micro-benchmark suite (BenchmarkDotNet) covers the allocation- and CPU-bearing surface: tag stamping, observer dispatch, and the clock seam. See [benchmarks.md](benchmarks.md). No measured numbers are committed; run the suite locally to produce them for your hardware.

## Packages

| Package                      | Purpose                                               |
| ---------------------------- | ----------------------------------------------------- |
| `Orion.Abstractions`         | The shared primitives above.                          |
| `Orion.Abstractions.Testing` | `FrozenOrionClock` and `RecordingObserver` test doubles. |

## Versioning

Follows [Semantic Versioning](https://semver.org/). **The 1.0 surface is frozen**: every contract in this README is source- and binary-compatible across the whole 1.x line, so a sibling package can bind to `1.*` and never break under a consumer's upgrade. Breaking changes wait for 2.0 and a migration note.

The library multi-targets `net8.0`, `net9.0`, and `net10.0`. (The benchmark host runs on net8.0 and net9.0 only, because BenchmarkDotNet 0.14.0 has no .NET 10 job moniker.)

## Documentation

- [docs/CONVENTIONS.md](docs/CONVENTIONS.md) - the normative rules every Orion package follows: options/DI shape, the time seam, telemetry naming, the error model, packaging and targets. Start here if you are building a package on the spine.
- [docs/FEATURES.md](docs/FEATURES.md) - a deeper breakdown of each feature and its public types and methods.
- [docs/observer-contract.md](docs/observer-contract.md) - the normative contract for what an Orion observer may and may not do.
- [docs/ROADMAP.md](docs/ROADMAP.md) - ideas under consideration.
- [benchmarks.md](benchmarks.md) - what the benchmark suite measures and how to run it.

## Contributing

Contributions are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md) and the [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).

## More from the Orion family

Focused .NET libraries built to one quality bar. Each is usable on its own; several share the small [`Orion.Abstractions`](https://github.com/tunahanaliozturk/Orion.Abstractions) contracts spine, but there is no deep dependency web — pick only what you need:

- [OrionGuard](https://github.com/tunahanaliozturk/OrionGuard) — validation, guard clauses, DDD primitives, domain events
- [OrionAudit](https://github.com/tunahanaliozturk/OrionAudit) — automatic EF Core change-audit trail
- [OrionBeacon](https://github.com/tunahanaliozturk/OrionBeacon) — leader election with fencing tokens
- [OrionClock](https://github.com/tunahanaliozturk/OrionClock) — testable time, TTLs, and deadlines
- [OrionGrant](https://github.com/tunahanaliozturk/OrionGrant) — permission / authorization checks
- [OrionKey](https://github.com/tunahanaliozturk/OrionKey) — source-generated strongly-typed IDs
- [OrionLedger](https://github.com/tunahanaliozturk/OrionLedger) — API-key issuance, verification, and rotation
- [OrionLens](https://github.com/tunahanaliozturk/OrionLens) — ambient correlation-context propagation
- [OrionLock](https://github.com/tunahanaliozturk/OrionLock) — distributed locks with fencing tokens
- [OrionOnce](https://github.com/tunahanaliozturk/OrionOnce) — idempotency keys for exactly-once request handling
- [OrionPatch](https://github.com/tunahanaliozturk/OrionPatch) — transactional outbox for EF Core
- [OrionRelay](https://github.com/tunahanaliozturk/OrionRelay) — outbound webhook delivery (HMAC, retries, backoff)
- [OrionResult](https://github.com/tunahanaliozturk/OrionResult) — Result/Option types and a shared error vocabulary
- [OrionSaga](https://github.com/tunahanaliozturk/OrionSaga) — sagas / process managers for long-running workflows
- [OrionShade](https://github.com/tunahanaliozturk/OrionShade) — sensitive-data redaction for logs and telemetry
- [OrionStream](https://github.com/tunahanaliozturk/OrionStream) — server-sent events / streaming hub
- [OrionVault](https://github.com/tunahanaliozturk/OrionVault) — field-level encryption for EF Core

See it all working together in [OrionShowcase](https://github.com/tunahanaliozturk/OrionShowcase), a production-shaped banking sample.

## License

[MIT](LICENSE).
