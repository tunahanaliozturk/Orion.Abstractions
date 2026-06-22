<!-- markdownlint-disable MD024 -->

# Changelog

All notable changes to Orion.Abstractions are documented in this file. The format is based
on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
