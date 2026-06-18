<!-- markdownlint-disable MD024 -->

# Changelog

All notable changes to Orion.Abstractions are documented in this file. The format is based
on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

[0.2.0]: https://github.com/tunahanaliozturk/Orion.Abstractions/releases/tag/v0.2.0
[0.1.0]: https://github.com/tunahanaliozturk/Orion.Abstractions/releases/tag/v0.1.0
