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

The full set of rules an observer must follow (no throwing from `onFault`, no blocking the host, cancellation semantics) is the [observer contract](observer-contract.md). `RecordingObserver<TObserver>` (in `Orion.Abstractions.Testing`, see below) lets a consumer assert their observers honor it.

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

## 4a. Recording observer test double

**Package:** `Orion.Abstractions.Testing`
**Namespace:** `Moongazing.Orion.Abstractions.Testing`
**Type:** `sealed class RecordingObserver<TObserver> where TObserver : class`

A recording double for `SafeObserverInvoker` call sites. It captures every observer invocation and every swallowed fault so a test can assert an observer (and the host driving it) behaves per the [observer contract](observer-contract.md). Compose it by passing `Track(...)` / `TrackAsync(...)` as the action and `OnFault` as the fault hook. The double never throws from the action or the fault hook itself, so it cannot perturb the fault-safety it verifies. All members are thread-safe.

| Member | Signature | Behavior |
| --- | --- | --- |
| constructors | `RecordingObserver()` / `RecordingObserver(TObserver? observer)` | The parameterless ctor leaves `Observer` null (drives the no-op path). The other supplies the observer the recorded action receives, or null. |
| `Observer` | `TObserver? Observer { get; }` | The observer instance handed to the recorded action, or null for the no-op (null-observer) path. |
| `Track` | `Action<TObserver> Track(Action<TObserver>? action = null)` | Wraps a sync action so each *completed* invocation is recorded. A faulting action records a fault (via `OnFault`) but no invocation. A null inner action records the invocation only. |
| `TrackAsync` | `Func<TObserver, Task> TrackAsync(Func<TObserver, Task>? action = null)` | The async counterpart. Records the invocation only after the returned task completes successfully, so a faulting or cancelled action records no invocation. |
| `OnFault` | `Action<Exception> OnFault { get; }` | The fault hook to hand to `SafeObserverInvoker` as its `onFault` argument. Records the swallowed exception and never throws. |
| `Invocations` / `InvocationCount` / `WasInvoked` | snapshots | The observer instances passed to the action in order, the count, and whether at least one ran. |
| `Faults` / `FaultCount` / `Faulted` | snapshots | The reported faults in order, the count, and whether at least one was reported. |
| `SingleFault` | `Exception SingleFault()` | The one reported fault, throwing `InvalidOperationException` if zero or more than one were recorded. |
| `Reset` | `void Reset()` | Clears recorded invocations and faults so the double can be reused across phases of a test. |

A propagated cancellation (the invoker re-throwing `OperationCanceledException` on a cancelled token) is *not* recorded as a fault, so the double can assert that cooperative shutdown is not downgraded.

## 5. Dependency-injection registration

**Namespace:** `Moongazing.Orion.Abstractions`
**Type:** `static class OrionAbstractionsServiceCollectionExtensions`

| Member | Signature | Behavior |
| --- | --- | --- |
| `AddOrionAbstractions` | `IServiceCollection AddOrionAbstractions(this IServiceCollection services)` | Registers `IOrionClock` as a `SystemOrionClock` singleton using `TryAddSingleton`, then returns the collection for chaining. Safe to call from multiple Orion packages' `Add` methods; the first registration (or a consumer override registered earlier) wins. |

## 6. The options and DI convention

**Namespace:** `Moongazing.Orion.Abstractions.Configuration`

One options shape for the whole family, so a package states its invariants once and every misconfiguration is reported the same way. Validation is *collecting*, not fail-fast: an implementation reports every invalid setting, so a misconfigured host fails once with a complete list rather than one round-trip per mistake.

### `abstract class OrionOptions`

| Member | Signature | Behavior |
| --- | --- | --- |
| `Validate` | `virtual void Validate(OrionOptionsValidationContext context)` | Reports each invariant violation to the context. The default reports nothing, so an options type with no invariants needs no override. |

`Validate` is **public**, not protected, on purpose: a protected member cannot be overridden as `protected internal` from another assembly (CS0507), which would trap every package author outside this one. Public also lets a test assert an options type's invariants directly, without a service provider.

Options types are mutable classes with parameterless constructors and usable defaults - `Microsoft.Extensions.Options` cannot bind an immutable record from configuration without a matching constructor shape.

### `sealed class OrionOptionsValidationContext`

| Member | Signature | Behavior |
| --- | --- | --- |
| `OptionsType` | `Type OptionsType { get; }` | The type being validated. |
| `OptionsName` | `string? OptionsName { get; }` | The named-options name, or null for the default instance, so a validator can relax an invariant for one named configuration. |
| `HasFailures` / `Failures` | `bool` / `IReadOnlyList<string>` | Whether anything failed, and the failures in report order. |
| `AddFailure` | `void AddFailure(string propertyName, string reason)` | Reports a failure against one setting as `{OptionsType}.{propertyName}: {reason}`. |
| `AddFailure` | `void AddFailure(string reason)` | Reports a cross-property failure with no single owning setting, as `{OptionsType}: {reason}`. |
| `Require` | `void Require(bool condition, string reason)` | Reports `reason` when the invariant does not hold. |
| `RequireNotNullOrWhiteSpace` | `void RequireNotNullOrWhiteSpace(string? value, string propertyName)` | Distinguishes unset (`must be set.`) from blank (`must not be empty or whitespace.`). |
| `RequirePositive` | `void RequirePositive(TimeSpan\|int value, string propertyName)` | Rejects zero and negative. |
| `RequireNonNegative` | `void RequireNonNegative(TimeSpan\|int value, string propertyName)` | Rejects negative; accepts zero. |
| `RequireInRange` | `void RequireInRange(TimeSpan\|int value, min, max, string propertyName)` | Bounds are inclusive. |

Numeric values are rendered with the invariant culture, so a failure message reads the same on every host locale.

### `sealed class OrionOptionsValidator<TOptions> : IValidateOptions<TOptions>`

Bridges `OrionOptions.Validate` onto the standard options pipeline. `Options.DefaultName` (the empty string) and null are both treated as the default instance, so an unnamed registration never fails with `named ''`.

### `static class OrionOptionsServiceCollectionExtensions`

| Member | Signature | Behavior |
| --- | --- | --- |
| `AddOrionOptions` | `OptionsBuilder<TOptions> AddOrionOptions<TOptions>(this IServiceCollection services, Action<TOptions>? configure = null, string? name = null) where TOptions : OrionOptions` | Configures the options and registers the validator with `TryAddEnumerable`. Calling it repeatedly (as a repeated `AddOrionX()` would) yields one validator while every configuration delegate still applies. Returns the builder for chaining. |

## 7. The telemetry naming surface

**Namespace:** `Moongazing.Orion.Abstractions.Diagnostics`
**Type:** `static class OrionTelemetry`

Two naming schemes, deliberately: instrumentation *scopes* use the assembly-style `Moongazing.OrionLock` so they match the package names an operator enables in their OTel config, while *metrics* and *tags* use the lowercase dotted form the OpenTelemetry semantic conventions mandate.

| Member | Signature | Behavior |
| --- | --- | --- |
| `ScopePrefix` / `MetricPrefix` | `const string` | `Moongazing.` and `orion.`. |
| `ScopeName` | `string ScopeName(string packageName)` | `ScopeName("OrionLock")` is `Moongazing.OrionLock`. Idempotent for an already-qualified name. |
| `MetricName` | `string MetricName(string component, string instrument)` | `MetricName("lock", "acquire.duration")` is `orion.lock.acquire.duration`. |
| `Tags` | `static class` | `orion.instance`, `orion.package`, `orion.tenant`, `orion.region`, `orion.operation`, `orion.outcome`, `orion.error.code`, `orion.attempt`. |
| `Outcomes` | `static class` | `success`, `failure`, `cancelled`, `timeout`. |

`OrionInstrumentation.InstanceTagKey` is an alias of `OrionTelemetry.Tags.Instance`; a test pins the two together so they cannot drift. The outcome set is bounded on purpose - outcome is a low-cardinality dimension, so failures are distinguished by `orion.error.code` rather than by inventing new outcomes.

## 8. The result and error vocabulary

**Namespace:** `Moongazing.Orion.Abstractions.Results`

Contract only. It gives the family one way to report an expected outcome - a lock already held, an idempotency key replayed, an API key expired - without the cost and control-flow damage of throwing. The ergonomic type with the map/bind/match surface ships in the `OrionResult` package.

### `readonly record struct OrionError`

| Member | Signature | Behavior |
| --- | --- | --- |
| `Code` | `string` | The stable machine-readable code consumers branch on. Lowercase `snake_case`, never localised, never reworded - it is an API surface. |
| `Message` | `string` | The human-readable description. Safe to log; not a UI string. |
| `Target` | `string?` | What the error is about - a field name, a resource id, a lock key. |
| `ToString` | `string ToString()` | `code: message`, or `code (target): message` when a target is set. |

The constructor rejects a blank code or message. Value equality, so two errors with the same code, message, and target compare equal.

### `static class OrionErrorCodes`

`invalid_argument`, `not_found`, `conflict`, `failed_precondition`, `unauthenticated`, `permission_denied`, `rate_limited`, `timeout`, `cancelled`, `unavailable`, `internal`. Mirrors the widely-understood canonical codes (gRPC / the Google API design guide) rather than inventing a fourth vocabulary, so the web tier maps an error to a status code without a per-package table. A genuinely package-specific condition prefixes its own code with the component's short name, e.g. `lock_lease_lost`.

### `interface IOrionResult` / `IOrionResult<out TValue>`

| Member | Signature | Behavior |
| --- | --- | --- |
| `IsSuccess` | `bool` | True when the operation achieved its intent. |
| `Error` | `OrionError?` | Non-null exactly when `IsSuccess` is false. |
| `Value` | `TValue` | Only meaningful on success; an implementation may throw when it is read on a failure. |

## Design constraints

- Multi-targets `net8.0`, `net9.0`, and `net10.0`.
- Nullable reference types enabled, `TreatWarningsAsErrors`, latest analyzers, documentation file generated.
- `IsAotCompatible` is set, so trim and AOT analyzers run on every build. A NativeAOT publish smoke test in CI publishes a native binary exercising every public entry point with `-warnaserror` and runs it.
- The runtime dependencies are `Microsoft.Extensions.DependencyInjection.Abstractions` and `Microsoft.Extensions.Options`. `Orion.Abstractions.Testing` adds only a project reference to `Orion.Abstractions`.
- No Orion dependencies, ever - that is what lets everything depend on it.
