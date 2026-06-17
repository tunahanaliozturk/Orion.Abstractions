# Features

A deeper breakdown of each primitive in **Orion.Abstractions**, the public types and members it exposes, and the contract each one guarantees. Everything documented here reflects the actual public API.

## 1. Fault-safe observer invocation

**Namespace:** `Moongazing.Orion.Abstractions.Observers`
**Type:** `static class SafeObserverInvoker`

Every Orion package exposes consumer observer hooks (dead-letter sinks, lock-event observers, encryption-audit observers, and so on). They all follow one contract, which this helper centralizes so the bespoke copies cannot drift:

- A `null` observer is a no-op; the call site is skipped.
- An observer fault is swallowed, so an observability outage can never break the load-bearing path the observer is attached to.
- `OperationCanceledException` always propagates when the supplied token is cancelled, so cooperative shutdown is never downgraded to a swallowed warning.

### Members

| Member | Signature | Behavior |
| --- | --- | --- |
| `Invoke` | `void Invoke<TObserver>(TObserver? observer, Action<TObserver> action, Action<Exception>? onFault = null) where TObserver : class` | Skips a null observer; invokes `action`; swallows any fault and passes it to `onFault`. |
| `InvokeAsync` | `Task InvokeAsync<TObserver>(TObserver? observer, Func<TObserver, Task> action, Action<Exception>? onFault = null, CancellationToken cancellationToken = default) where TObserver : class` | The async counterpart. Re-throws `OperationCanceledException` when `cancellationToken` is cancelled; swallows all other faults to `onFault`. Awaits with `ConfigureAwait(false)`. |
| `Resolve` | `void Resolve<TObserver>(Func<TObserver?> resolve, Action<TObserver> action, Action<Exception>? onFault = null) where TObserver : class` | Runs the `resolve` factory *inside* the fault guard, so a throwing observer constructor or DI dependency cannot abort the host path at resolution time. A null factory result skips `action`. |

`action` and `resolve` are validated with `ArgumentNullException.ThrowIfNull`; passing null for either throws (this is a programming error, not an observer fault). The `onFault` callback is optional and must not throw.

## 2. OpenTelemetry instrumentation conventions

**Namespace:** `Moongazing.Orion.Abstractions.Diagnostics`
**Type:** `abstract class OrionInstrumentation : IDisposable`

A base class so every family member instruments the same way. A member derives a sealed diagnostics class, creates its instruments on the shared `Meter`, and stamps measurements through `Tag(...)`.

### Members

| Member | Signature | Behavior |
| --- | --- | --- |
| constructor | `protected OrionInstrumentation(string name, string version)` | Creates an `ActivitySource` and a `Meter` that share `name` and `version`. Both `name` and `version` are validated with `ArgumentException.ThrowIfNullOrEmpty`. |
| `ActivitySource` | `ActivitySource { get; }` | The activity source members write spans into. |
| `Meter` | `Meter { get; }` | The meter members create counters, histograms, and gauges on. |
| `StaticTags` | `KeyValuePair<string, object?>[] StaticTags { get; }` | A snapshot of the configured static tags. Defaults to empty. Exposed as a snapshot so an emission site never observes a half-built array. |
| `SetStaticTags` | `void SetStaticTags(IReadOnlyDictionary<string, string> tags)` | Replaces the static tag set. Intended to be called once at startup, single-threaded. Tags set after the host starts emitting do not retroactively apply to already-emitted measurements. |
| `Tag` | `KeyValuePair<string, object?>[] Tag(KeyValuePair<string, object?> extra)` | Combines the static tags with one per-measurement tag. When no static tags are configured it returns a single-element array, keeping the common untagged path allocation-light. |
| `Dispose` | `void Dispose()` / `protected virtual void Dispose(bool disposing)` | Disposes the `ActivitySource` and `Meter`. Override the protected overload to dispose extra resources. |

The static-tag field is `volatile` and replaced wholesale by `SetStaticTags`, so a concurrent reader either sees the old snapshot or the new one, never a partially populated array.

### Why static tags rather than a second Meter

Stamping a tenant/region/environment tag onto every measurement lets dashboards split by that dimension without standing up a second `Meter`. `Tag(...)` is on the hot path of every metric emission, which is why the no-static-tags case short-circuits.

## 3. The clock abstraction

**Namespace:** `Moongazing.Orion.Abstractions.Time`

### `interface IOrionClock`

A minimal clock seam every Orion background worker, lease, and scheduler can depend on for deterministic testing. It is intentionally a thin layer over `TimeProvider` rather than a re-implementation, so the family shares one clock contract and one DI registration name instead of each package declaring its own `IClock`.

| Member | Signature | Behavior |
| --- | --- | --- |
| `UtcNow` | `DateTimeOffset UtcNow { get; }` | The current UTC instant. |
| `GetTimestamp` | `long GetTimestamp()` | A high-frequency monotonic timestamp, in `Stopwatch.GetTimestamp` units. |
| `GetElapsedTime` | `TimeSpan GetElapsedTime(long startingTimestamp)` | Elapsed time since a prior `GetTimestamp` value. Prefer this over subtracting `UtcNow` values, which are subject to wall-clock adjustment. |

### `sealed class SystemOrionClock : IOrionClock`

The production implementation, forwarding to a `TimeProvider`.

- `SystemOrionClock()` - over `TimeProvider.System`.
- `SystemOrionClock(TimeProvider timeProvider)` - over an explicit provider (null is rejected with `ArgumentNullException`).

## 4. Deterministic test clock

**Package:** `Orion.Abstractions.Testing`
**Namespace:** `Moongazing.Orion.Abstractions.Testing`
**Type:** `sealed class FrozenOrionClock : IOrionClock`

A deterministic clock for tests. The wall clock and the monotonic timestamp are both frozen at construction and only move when you advance them, so a test can drive lease expiry, grace periods, and scheduled work without real delays or flakiness.

| Member | Signature | Behavior |
| --- | --- | --- |
| constructor | `FrozenOrionClock(DateTimeOffset? start = null)` | Starts frozen at `start`, defaulting to `2026-01-01Z`. The monotonic timestamp starts at zero. |
| `Advance` | `void Advance(TimeSpan delta)` | Advances both the wall clock and the monotonic timestamp by `delta`. Negative deltas are rejected with `ArgumentOutOfRangeException`, matching a real monotonic clock. |
| `SetUtcNow` | `void SetUtcNow(DateTimeOffset value)` | Sets the wall clock to an explicit instant without moving the monotonic timestamp. Rejects a value earlier than the current one. |
| `UtcNow` / `GetTimestamp` / `GetElapsedTime` | (from `IOrionClock`) | Read the frozen state. `GetElapsedTime` is computed from the monotonic timestamp using `Stopwatch.Frequency`. |

## 5. Dependency-injection registration

**Namespace:** `Moongazing.Orion.Abstractions`
**Type:** `static class OrionAbstractionsServiceCollectionExtensions`

| Member | Signature | Behavior |
| --- | --- | --- |
| `AddOrionAbstractions` | `IServiceCollection AddOrionAbstractions(this IServiceCollection services)` | Registers `IOrionClock` as a `SystemOrionClock` singleton using `TryAddSingleton`, then returns the collection for chaining. Safe to call from multiple Orion packages' `Add` methods; the first registration (or a consumer override registered earlier) wins. |

## Design constraints

- Multi-targets `net8.0`, `net9.0`, and `net10.0`.
- Nullable reference types enabled, `TreatWarningsAsErrors`, latest analyzers, documentation file generated.
- The only runtime dependency is `Microsoft.Extensions.DependencyInjection.Abstractions`. `Orion.Abstractions.Testing` adds only a project reference to `Orion.Abstractions`.
