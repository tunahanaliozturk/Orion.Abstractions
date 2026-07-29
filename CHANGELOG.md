<!-- markdownlint-disable MD024 -->

# Changelog

All notable changes to Orion.Abstractions are documented in this file. The format is based
on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.2.0] - 2026-07-29

Wave 2 (Reliability): the **deadline seam**. Additive to the core — everything in 1.0/1.1 is
unchanged, so this stays a drop-in upgrade for the 1.x line.

### Added

- **`OrionDeadline`** (`Moongazing.Orion.Abstractions.Time`) — a time budget expressed as a monotonic
  deadline against an `IOrionClock`. `OrionDeadline.After(clock, budget)` captures the clock's
  monotonic timestamp; `IsExpired(clock)` and `Remaining(clock)` evaluate against it (immune to
  wall-clock adjustments), and `OrionDeadline.Never` / an infinite budget model "no timeout". It
  replaces the ad-hoc `Stopwatch`-elapsed and `CancellationTokenSource.CancelAfter` patterns in the
  family's acquire / retry / renew loops with one shape that is **deterministic under
  `FrozenOrionClock`** — advance the clock to expire it, so timeout logic is testable without real
  waits. Verified trim/AOT-clean via the AOT smoke test.

## [1.1.0] - 2026-07-29

Wave 2 (Reliability) groundwork, additive to the **testing companion** only. The core
`Orion.Abstractions` API is unchanged from 1.0.0 — still source- and binary-compatible for the whole
1.x line — so this is a drop-in upgrade; the version moves to 1.1.0 because
`Orion.Abstractions.Testing` gained new helpers.

### Added

- **`DeterministicFaultInjector`** (`Orion.Abstractions.Testing`) — a reproducible, no-randomness fault
  injector for reliability tests (retry, backoff, exactly-once, circuit-breaking). Schedules:
  `FailFirst(n)`, `FailOnAttempts(...)`, `AlwaysFail()`, `NeverFail()`, `FailUntil(clock, instant)`
  (time-based recovery over `FrozenOrionClock`), and `When(predicate)`. Call `Next()` at each attempt
  or wrap the operation with `Run` / `RunAsync`; attempt counting is thread-safe. Injected faults are
  a dedicated `DeterministicFaultException` so a test can catch them distinctly. This is the
  deterministic fault-injection helper the family's Wave 2 exactly-once chaos tests build on.
- **`RecordingObserver<TObserver>.Events`** — an interleaved, ordered timeline of invocations and
  swallowed faults (`RecordedEvent` / `RecordedEventKind`). Lets a test assert ordering the separate
  `Invocations` / `Faults` lists cannot express — e.g. that a fault occurred *between* two successful
  invocations. `Reset()` clears it alongside the existing lists.

## [1.0.0] - 2026-07-20

The spine is frozen. Everything the family binds to - the options/DI shape, the telemetry
naming surface, the error vocabulary, the time seam, the observer contract - is now source- and
binary-compatible for the whole 1.x line, so a sibling package can reference `1.*` and never
break under a consumer's upgrade. This is the anchor for the family's Wave 1: every sibling's
Wave 1 release binds to this version.

### Added

- **The options and DI convention** (`Moongazing.Orion.Abstractions.Configuration`). Every
  package's `OrionXOptions` now derives from one base type and states its invariants once:
  - `OrionOptions`: the base every options type derives from. `Validate` is `public virtual`,
    not `protected` - a protected member would force every package author outside this assembly
    into the `protected internal` override trap (CS0507), and a public one lets a test assert an
    options type's invariants directly without a service provider.
  - `OrionOptionsValidationContext`: the collector an implementation reports violations to, with
    `AddFailure`, `Require`, `RequireNotNullOrWhiteSpace`, `RequirePositive`,
    `RequireNonNegative`, and `RequireInRange` for durations and counts. The constructor and the
    `For<TOptions>()` factory are public, and `BuildFailureMessage()` returns the exact
    operator-facing text - so a package can unit-test its own `Validate` override directly
    rather than through a service provider. Validation *collects*
    rather than fail-fast, so a misconfigured host learns every mistake in one startup instead
    of one per restart, and it owns the failure-message format so all sixteen packages reject
    configuration identically.
  - `OrionOptionsValidator<TOptions>`: bridges the above onto `IValidateOptions<TOptions>`, so
    the standard options pipeline enforces the invariants. Treats `Options.DefaultName` (the
    empty string) and null as the same "default instance", so an unnamed registration never
    fails with `named ''`.
  - `AddOrionOptions<TOptions>(configure, name)`: the one call an `AddOrionX()` method makes.
    Registers the validator with `TryAddEnumerable`, so calling `AddOrionX()` twice yields one
    validator while both configuration delegates still apply. Returns the `OptionsBuilder<T>`
    for chaining (including `ValidateOnStart()` where the hosting package is referenced).
- **The OpenTelemetry naming surface** (`OrionTelemetry`). The frozen constants and helpers that
  replace per-package magic strings: `ScopeName(package)` for the assembly-style
  `Moongazing.OrionLock` instrumentation scope, `MetricName(component, instrument)` for the
  lowercase dotted `orion.lock.acquire.duration` metric name, `Tags` (`orion.instance`,
  `orion.package`, `orion.tenant`, `orion.region`, `orion.operation`, `orion.outcome`,
  `orion.error.code`, `orion.attempt`), and `Outcomes` (`success`, `failure`, `cancelled`,
  `timeout`) as a deliberately bounded low-cardinality vocabulary - failures are distinguished
  by error code, not by inventing new outcomes.
- **The result and error vocabulary** (`Moongazing.Orion.Abstractions.Results`). Contract only;
  the ergonomic type with map/bind/match ships in the `OrionResult` package.
  - `OrionError`: a readonly record struct carrying a machine-readable `Code`, a human-readable
    `Message`, and an optional `Target`. Lets a package report an expected outcome - a lock
    already held, an idempotency key replayed, an API key expired - without the cost and
    control-flow damage of throwing.
  - `OrionErrorCodes`: the canonical codes (`invalid_argument`, `not_found`, `conflict`,
    `failed_precondition`, `unauthenticated`, `permission_denied`, `rate_limited`, `timeout`,
    `cancelled`, `unavailable`, `internal`), mirroring the widely-understood gRPC/Google
    vocabulary rather than inventing a fourth one, so the web tier can map an error to a status
    code without a per-package lookup table.
  - `IOrionResult` / `IOrionResult<TValue>`: the shape cross-cutting code (telemetry stamping,
    `ProblemDetails` mapping, envelope rendering) reads without knowing which package produced
    the outcome.
- **`IOrionClock.GetUtcNow()`**, a default interface method forwarding to `UtcNow`, so the
  contract mirrors `TimeProvider.GetUtcNow()` and code moves between the two seams without a
  rename. Non-breaking for existing implementations.
- **`docs/CONVENTIONS.md`**: the normative rules every package follows - the `AddOrionX()` and
  options shape, the time seam, telemetry naming, the error model, observers, packaging and
  targets, versioning - plus the Wave 1 checklist a sibling's release PR is reviewed against.
- **A NativeAOT publish smoke test** (`tests/Moongazing.Orion.Abstractions.AotSmoke`) and the
  `aot-smoke` CI job. It publishes a fully-native binary that exercises every public entry point
  - DI registration, options configuration and validation failure, both clock surfaces,
  instrumentation construction and tag stamping, the observer guard, error rendering - with
  `-warnaserror`, then runs it. The release publish job now depends on it.

### Changed

- `OrionInstrumentation.InstanceTagKey` is now an alias of `OrionTelemetry.Tags.Instance`. The
  literal value (`orion.instance`) is unchanged, and a test pins the two together so they cannot
  drift.
- The package now takes `Microsoft.Extensions.Options` (9.0.0) alongside
  `Microsoft.Extensions.DependencyInjection.Abstractions`, for the options convention. Still no
  Orion dependencies, and still no reflection-based binding.
- `IsAotCompatible` is set on the library, so trim and AOT analyzers run on every build rather
  than only at publish.

## [0.3.0] - 2026-06-22

### Added

A normative observer contract and a recording observer test double. These pair together: the
contract states what an Orion observer may and may not do, and the test double lets a consumer
assert their observers honor it. No production API changed; both additions are documentation
and a testing helper.

- `docs/observer-contract.md`: the normative contract for any consumer hook invoked through
  `SafeObserverInvoker` (dead-letter sinks, lock-event observers, encryption-audit observers,
  and so on). It states the host-protecting guarantees the invoker already makes (null observer
  is a no-op, observer faults are swallowed, resolution faults are swallowed, cancellation
  propagates only on a cancelled token, a null `action` still throws) and the rules an observer
  author owns (an `onFault` must not throw, an observer must not block the host, an async
  observer must honor the cancellation token). Linked from the README, FEATURES, and the
  `Orion.Abstractions.Testing` package readme.
- `RecordingObserver<TObserver>` in `Orion.Abstractions.Testing`: a recording double for
  `SafeObserverInvoker` call sites. `Track` / `TrackAsync` wrap the action so completed
  invocations are recorded; `OnFault` records swallowed faults; assertion surface includes
  `Invocations`, `InvocationCount`, `WasInvoked`, `Faults`, `FaultCount`, `Faulted`,
  `SingleFault()`, and `Reset()`. A propagated cancellation is not recorded as a fault, so the
  double can assert cooperative shutdown is not downgraded. All members are thread-safe.

### Tests

Added `RecordingObserverTests` covering the double against all three `SafeObserverInvoker`
entry points (`Invoke`, `InvokeAsync`, `Resolve`): completed invocations, swallowed sync and
async faults, the null-observer no-op, propagated cancellation not recorded as a fault, ordered
multi-invocation recording, `SingleFault` arity, `Reset`, and snapshot independence.

## [0.2.0] - 2026-06-19

### Added

Instance-scoped instrumentation for `OrionInstrumentation`. Every Orion diagnostics class
names its `Meter` by a shared constant, so a name-filtered `MeterListener` cannot tell two
live instances apart and double-counts when a process holds several instances or when tests
run in parallel. This release adds a non-breaking way to disambiguate:

- New opt-in `protected OrionInstrumentation(string name, string version, string? instanceScopeId,
  IReadOnlyDictionary<string, string>? instanceTags = null, object? scope = null)` constructor.
  When `instanceScopeId` is supplied, the `Meter` is created via `MeterOptions` carrying a
  stable `orion.instance` tag and a scope object (synthesized when none is passed), using the
  .NET 8+ `Meter(MeterOptions)` constructor.
- `OrionInstrumentation.InstanceTagKey` constant (`orion.instance`) and the
  `string? InstanceScopeId` property surfacing the per-instance id.
- `static bool OrionInstrumentation.ListensTo(Instrument, OrionInstrumentation)` predicate that
  filters a `MeterListener` to exactly one instance's instruments by reference identity of the
  owning `Meter` (`ReferenceEquals(instrument.Meter, instance.Meter)`).

The existing parameterless name/version constructor is unchanged and remains the default: no
instance tag, no scope. The opt-in constructor with all-null arguments is equivalent to it.

### Tests

Added instance-scope unit tests plus `MeterListener` integration tests (in a non-parallel
collection, each filtered to a specific instrument/meter instance) verifying that two instances
sharing one Meter name are independently observable and not double-counted.

## [0.1.0] - 2026-06-14

### Added

Initial release. The shared foundation layer for the Orion family.

- `SafeObserverInvoker`: fault-safe observer invocation (sync + async + resolve-in-guard).
  Null observers are no-ops, observer faults are swallowed, `OperationCanceledException`
  propagates on cancellation.
- `OrionInstrumentation`: base class for OpenTelemetry instrumentation - a consistently-named
  `ActivitySource` + `Meter` plus the static-tag stamping pattern (`SetStaticTags` / `Tag`).
- `IOrionClock` + `SystemOrionClock`: a thin `TimeProvider` seam shared across Orion packages.
- `AddOrionAbstractions()` DI extension (registers `IOrionClock` -> `SystemOrionClock` via TryAdd).
- `Orion.Abstractions.Testing` package: `FrozenOrionClock` for deterministic time in tests.

### Tests

13 facts across observer invocation, instrumentation, and the frozen clock.

[0.3.0]: https://github.com/tunahanaliozturk/Orion.Abstractions/releases/tag/v0.3.0
[0.2.0]: https://github.com/tunahanaliozturk/Orion.Abstractions/releases/tag/v0.2.0
[0.1.0]: https://github.com/tunahanaliozturk/Orion.Abstractions/releases/tag/v0.1.0
