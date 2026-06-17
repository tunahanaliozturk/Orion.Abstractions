# Roadmap

This is a list of ideas under consideration, not a set of commitments or dated milestones. Items may change, be reordered, or be dropped as the Orion family's needs become clearer. The guiding principle is that a primitive only earns a place here once it has been re-implemented (and has drifted) across enough family members to justify being shared.

## Under consideration

- **More test doubles in `Orion.Abstractions.Testing`.** `FrozenOrionClock` is the first. A recording or asserting observer double for `SafeObserverInvoker` call sites could remove similar boilerplate from family test suites.
- **Family convergence.** Existing Orion packages still carry their own copies of these primitives. Migrating them onto the shared abstractions is the main thing that would validate the contracts here and surface any gaps.
- **Static-tag ergonomics.** The current `SetStaticTags` / `Tag` pattern is deliberately minimal. If real usage shows a recurring need (for example, a builder for the startup tag set, or a typed tag helper), it is worth revisiting carefully without regressing the allocation-light hot path.
- **Documentation of the observer contract.** As more family members adopt `SafeObserverInvoker`, a short written contract for what an Orion observer may and may not do (no throwing from `onFault`, no blocking the host) would help consumers implement hooks correctly.
- **Benchmark coverage on newer runtimes.** The benchmark host currently runs on net8.0 and net9.0 because the pinned BenchmarkDotNet version has no .NET 10 job moniker. Revisiting this once tooling support lands would let the benchmarks exercise every target the library ships.

## Explicitly out of scope

- Anything that adds a runtime dependency beyond `Microsoft.Extensions.DependencyInjection.Abstractions`. Keeping this package dependency-light is the reason other Orion libraries can safely depend on it.
- Re-implementing functionality that `TimeProvider`, `ActivitySource`, or `Meter` already provide. The value here is shared *conventions* over those primitives, not replacements for them.

Have a primitive that keeps getting re-implemented across Orion packages? That is exactly the kind of thing this package exists for. See [CONTRIBUTING.md](../CONTRIBUTING.md).
