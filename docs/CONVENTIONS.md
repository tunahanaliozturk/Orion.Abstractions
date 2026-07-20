# Orion conventions (frozen at Abstractions 1.0)

The rules every package in the Orion family follows. They are frozen at `Orion.Abstractions`
1.0: a package that binds to 1.x can rely on every shape here staying source- and
binary-compatible until the next major.

This document is normative. Where it says MUST, a deviation is a bug in the package, not a
style preference. The companion document [`observer-contract.md`](observer-contract.md) is
normative for observers specifically.

---

## 1. Options and DI

### The shape

Every package exposes exactly one entry point per feature, named `AddOrionX()`, taking an
optional configuration delegate, and returning `IServiceCollection` for chaining.

```csharp
public static IServiceCollection AddOrionLock(
    this IServiceCollection services,
    Action<OrionLockOptions>? configure = null)
{
    ArgumentNullException.ThrowIfNull(services);

    services.AddOrionAbstractions();              // the spine, always
    services.AddOrionOptions(configure);          // options + validation, always
    services.TryAddSingleton<IOrionLock, RedisOrionLock>();

    return services;
}
```

Rules:

- **MUST** call `AddOrionAbstractions()` so the clock seam is registered.
- **MUST** register services through `TryAdd*` / `TryAddEnumerable`, never `Add*`. A consumer
  who registered their own implementation first wins; calling `AddOrionX()` twice is a no-op,
  not a duplicate registration.
- **MUST NOT** build a `ServiceProvider`, resolve services, or touch configuration during
  registration. Registration is declarative.
- **MUST NOT** require a hosted service for the package's core value. A package may offer one;
  it may not depend on one to function.

### Options types

Every options type derives from `OrionOptions`, is a mutable class with a parameterless
constructor and usable defaults, and expresses its invariants in one `Validate` override.

```csharp
public sealed class OrionLockOptions : OrionOptions
{
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan RenewalInterval { get; set; } = TimeSpan.FromSeconds(10);
    public string KeyPrefix { get; set; } = "orion:lock:";

    public override void Validate(OrionOptionsValidationContext context)
    {
        base.Validate(context);

        context.RequirePositive(LeaseDuration, nameof(LeaseDuration));
        context.RequirePositive(RenewalInterval, nameof(RenewalInterval));
        context.RequireNotNullOrWhiteSpace(KeyPrefix, nameof(KeyPrefix));
        context.Require(
            RenewalInterval < LeaseDuration,
            "RenewalInterval must be shorter than LeaseDuration, or a lease expires before it renews.");
    }
}
```

Rules:

- **MUST** report *every* violation to the context rather than throwing on the first one. A
  misconfigured host should learn all its mistakes in one startup, not one per restart.
- **MUST** be bindable from `IConfiguration` — mutable properties, no required constructor
  parameters, no immutable records.
- **MUST** default to safe, working values wherever a default is meaningful, so
  `AddOrionX()` with no delegate is a valid production configuration unless the package
  genuinely needs a secret or connection string.
- **SHOULD** use `TimeSpan` for durations, never `int` milliseconds.

Failure messages come out in one frozen format, so an operator sees the same thing whichever
package rejected their config:

```
OrionLockOptions is invalid:
  - OrionLockOptions.LeaseDuration: must be greater than zero (was 00:00:00).
  - OrionLockOptions: RenewalInterval must be shorter than LeaseDuration, or a lease expires before it renews.
```

---

## 2. Time

There is one time seam in the family: `IOrionClock`.

- **MUST NOT** call `DateTime.UtcNow`, `DateTimeOffset.UtcNow`, `Stopwatch.StartNew`, or
  `Environment.TickCount` in package code. Inject `IOrionClock`.
- **MUST** measure durations with `GetTimestamp()` / `GetElapsedTime(start)`, never by
  subtracting two `UtcNow` reads — a wall-clock adjustment mid-operation makes that negative.
- **MUST NOT** declare a package-local `IClock`. The whole point is that one fake clock
  advances the entire suite in a test.
- `UtcNow` and `GetUtcNow()` are the same value; use whichever reads better at the call site.

Tests take `FrozenOrionClock` from `Orion.Abstractions.Testing`. A test that sleeps to wait for
an expiry is a bug: advance the clock instead.

---

## 3. Telemetry

All names come from `OrionTelemetry`. No package declares a naming magic string.

```csharp
internal sealed class LockInstrumentation()
    : OrionInstrumentation(OrionTelemetry.ScopeName("OrionLock"), ThisAssembly.Version)
{
    public Histogram<double> AcquireDuration { get; } =
        Meter.CreateHistogram<double>(OrionTelemetry.MetricName("lock", "acquire.duration"), "ms");
}
```

- Instrumentation **scope** names (`ActivitySource` + `Meter`) are `Moongazing.OrionX` — build
  them with `OrionTelemetry.ScopeName`.
- **Metric** names are `orion.{component}.{instrument}`, lowercase and dotted — build them with
  `OrionTelemetry.MetricName`.
- Tag keys covered by `OrionTelemetry.Tags` **MUST** use those constants. A package adds its own
  domain tags freely, but never re-spells `orion.tenant` as `tenant_id`.
- The `orion.outcome` tag **MUST** take a value from `OrionTelemetry.Outcomes`. Outcome is a
  low-cardinality dimension; distinguish failures with `orion.error.code`, not new outcomes.
- **MUST NOT** put unbounded values (ids, keys, user input) in a *metric* tag. Those belong on
  a span attribute.

---

## 4. Errors and results

- Expected outcomes **MUST NOT** throw. A lock already held, an idempotency key replayed, an
  API key expired — these are results, not exceptions.
- Genuinely exceptional conditions still throw: a broken invariant, a misconfiguration, an
  unreachable dependency the package cannot model as an outcome.
- Argument validation throws `ArgumentException` / `ArgumentNullException` — that is a caller
  bug, not a domain outcome.
- An outcome type implements `IOrionResult` / `IOrionResult<T>`, and its failure carries an
  `OrionError`.
- Error codes **SHOULD** come from `OrionErrorCodes`. A package-specific code is prefixed with
  the component's short name, e.g. `lock_lease_lost`. Codes are lowercase `snake_case`, stable
  across versions, and never localised — they are an API surface.

---

## 5. Observers

See [`observer-contract.md`](observer-contract.md) — normative and unchanged at 1.0. In short:
every consumer-supplied hook goes through `SafeObserverInvoker`, a null observer is a no-op,
observer faults are swallowed, resolution happens inside the guard, and cancellation always
propagates.

---

## 6. Packaging and targets

- **MUST** multi-target `net8.0;net9.0;net10.0`.
- **MUST** be AOT- and trim-clean: `<IsAotCompatible>true</IsAotCompatible>`, zero IL2xxx/IL3xxx
  warnings, and a NativeAOT publish smoke test in CI that runs the resulting native binary.
- **MUST** prefer source generation and explicit registration over reflection and assembly
  scanning.
- **MUST** use `[GeneratedRegex]` for any regex.
- **MUST** enable nullable reference types and `TreatWarningsAsErrors`.
- **MUST NOT** take a runtime dependency on a sibling Orion package. Integration ships as a
  separate bridge package (`OrionX.Extensions.OrionY`).
- `Orion.Abstractions` is the only allowed suite-wide dependency, referenced as `1.*`.

---

## 7. Versioning

- Semantic versioning. Breaking changes only in a major, batched at a wave boundary.
- Every release ships release notes, a CHANGELOG entry, and a migration note when a consumer
  has to change code.
- A package never force-upgrades a sibling. `Orion.Abstractions` majors are the family's only
  coordination point.

---

## Checklist for a package's Wave 1 PR

- [ ] References `Orion.Abstractions` 1.x and calls `AddOrionAbstractions()` from `AddOrionX()`.
- [ ] Options derive from `OrionOptions`, validate through `OrionOptionsValidationContext`, and
      are registered with `AddOrionOptions`.
- [ ] No `DateTime.UtcNow` / `Stopwatch` / package-local `IClock` anywhere in package code.
- [ ] Every telemetry name and shared tag key comes from `OrionTelemetry`.
- [ ] Every consumer hook routes through `SafeObserverInvoker`.
- [ ] Multi-targets `net8.0;net9.0;net10.0`; `IsAotCompatible` set; NativeAOT smoke job green.
- [ ] All registrations use `TryAdd*`; calling `AddOrionX()` twice is a no-op.
