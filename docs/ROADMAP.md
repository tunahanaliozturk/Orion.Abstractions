# Roadmap

Current released version: **0.3.0**. It adds a normative observer contract and a recording observer test double on top of the 0.2.0 release (instance-scoped instrumentation) and the 0.1.0 foundation (fault-safe observer invocation, OpenTelemetry instrumentation conventions, and a testable clock).

This is a list of directions under consideration, not a set of commitments. Items may change, be reordered, or be dropped as the Orion family's needs become clearer. The guiding principle is unchanged: a primitive only earns a place here once it has been re-implemented (and has drifted) across enough family members to justify being shared. Version milestones below indicate ordering and intent, not promised dates.

## Released / Recently shipped

- **0.3.0** - A normative observer contract and a recording observer test double. `docs/observer-contract.md` states what an Orion observer invoked through `SafeObserverInvoker` may and may not do (an `onFault` must not throw, an observer must not block the host, an async observer must honor the cancellation token), grounded in the guarantees the invoker already makes. `RecordingObserver<TObserver>` joins `FrozenOrionClock` in `Orion.Abstractions.Testing`: it records observer invocations and swallowed faults at a `SafeObserverInvoker` call site (`Track` / `TrackAsync` as the action, `OnFault` as the fault hook) so consumers can assert their observers honor the contract. No production API changed.
- **0.2.0** - Instance-scoped instrumentation for `OrionInstrumentation`. An opt-in scoped constructor names the `Meter` via `MeterOptions` carrying a stable `orion.instance` tag (`InstanceTagKey`) plus any custom instance tags and a scope object, so a listener can target exactly one of two live instances that share a Meter name. A name-only `MeterListener` still observes both instances; isolation comes from the new static `ListensTo(Instrument, OrionInstrumentation)` predicate (backed by the `InstanceScopeId` property and the instance tag), which filters a listener to one instance's instruments by Meter reference identity. The parameterless name/version constructor is unchanged and remains the default (no instance tag, no scope).
- **0.1.0** - Initial release. `SafeObserverInvoker` (fault-safe sync/async/resolve-in-guard observer invocation), `OrionInstrumentation` (consistently named `ActivitySource` + `Meter` with the `SetStaticTags` / `Tag` stamping pattern), `IOrionClock` + `SystemOrionClock` (a thin `TimeProvider` seam), the `AddOrionAbstractions()` DI extension, and the `Orion.Abstractions.Testing` package with `FrozenOrionClock`.

## Next

The 0.3.0 written observer contract and recording observer test double have shipped (see above).
These remain under consideration, unscheduled:

- **Family convergence.** Existing Orion packages still carry their own copies of these primitives. Migrating them onto the shared abstractions is the main thing that would validate the contracts here and surface gaps, and it is the highest-signal driver for everything below. No primitive graduates from "under consideration" until convergence shows it is genuinely shared. This is a cross-repo effort, not a change to this package.
- **Static-tag ergonomics, only if usage demands it.** The `SetStaticTags` / `Tag` pattern is deliberately minimal. If convergence shows a recurring need (for example, a builder for the startup tag set, or a typed tag helper), it is worth revisiting carefully without regressing the allocation-light hot path. Held back deliberately until there is evidence, not added speculatively.

## Later (theme, post-0.3.0)

These fit an instrumentation/abstractions base and are tracked, but none is scheduled. Each would land only if family usage justifies it and it can be done without taking on a new runtime dependency.

- **Diagnostics and metrics helpers.** Small, allocation-conscious helpers for common emission shapes (for example, a guarded measurement-recording helper, or counter/histogram conventions) that several family members would otherwise re-implement on top of `Meter`. Conventions over `Meter`, never a wrapper that hides it.
- **Activity and baggage helpers.** Thin conventions for starting and tagging an `Activity` consistently, and for propagating a small set of well-known baggage keys, aligned with how `OrionInstrumentation` already names its `ActivitySource`. Conventions over `ActivitySource`, not a replacement.
- **Options-validation helpers.** If multiple family members converge on the same `IValidateOptions<T>` / `ValidateOnStart` shapes for their configuration, a shared validation helper could live here. Gated on the dependency rule below: only if it stays within the existing `Microsoft.Extensions.*.Abstractions` surface.
- **AOT and trimming posture.** Audit the public surface for trimming and Native AOT friendliness, annotate where needed, and document the supported posture so consumers building trimmed or AOT apps know what they can rely on. The library is already nullable-enabled with warnings as errors; this extends that rigor to AOT.
- **Analyzer support.** A small analyzer or set of conventions that flag misuse at the call site (for example, a faulting `onFault`, bypassing `Tag(...)` on an emission, or supplying the reserved `orion.instance` key) could enforce the contracts mechanically. Only worthwhile once the contracts are written and stable, and would ship as an optional companion, never a new runtime dependency on the core package.

## Tooling

- **Benchmark coverage on newer runtimes.** The benchmark host currently runs on net8.0 and net9.0 only, because the pinned BenchmarkDotNet version has no .NET 10 job moniker. Revisiting this once tooling support lands would let the benchmarks exercise every target the library ships (the library already multi-targets net8.0, net9.0, and net10.0).

## Explicitly out of scope

- Anything that adds a runtime dependency beyond `Microsoft.Extensions.DependencyInjection.Abstractions`. Keeping this package dependency-light is the reason other Orion libraries can safely depend on it. Any forward item above that cannot meet this bar ships as an optional companion package or not at all.
- Re-implementing functionality that `TimeProvider`, `ActivitySource`, or `Meter` already provide. The value here is shared *conventions* over those primitives, not replacements for them.

Have a primitive that keeps getting re-implemented across Orion packages? That is exactly the kind of thing this package exists for. See [CONTRIBUTING.md](../CONTRIBUTING.md).
