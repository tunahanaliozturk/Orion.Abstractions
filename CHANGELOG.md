<!-- markdownlint-disable MD024 -->

# Changelog

All notable changes to Orion.Abstractions are documented in this file. The format is based
on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

[0.1.0]: https://github.com/tunahanaliozturk/Orion.Abstractions/releases/tag/v0.1.0
