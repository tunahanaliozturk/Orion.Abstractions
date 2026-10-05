# Orion.Abstractions

The shared spine of the Orion family of .NET libraries: fault-safe observer invocation, OpenTelemetry naming and instrumentation, a testable clock and deadline, the options/DI convention, and the error vocabulary. Depend on it from any library that wants the Orion conventions; it has no Orion dependencies of its own.

![Orion.Abstractions and Orion.Abstractions.Testing: what each package holds and who depends on them](https://raw.githubusercontent.com/tunahanaliozturk/Orion.Abstractions/main/docs/diagrams/overview.png)

## Install

```bash
dotnet add package Orion.Abstractions
```

## Quick start

```csharp
using Microsoft.Extensions.DependencyInjection;
using Moongazing.Orion.Abstractions;
using Moongazing.Orion.Abstractions.Time;

var services = new ServiceCollection();
services.AddOrionAbstractions(); // IOrionClock -> SystemOrionClock, via TryAddSingleton

using var provider = services.BuildServiceProvider();
var clock = provider.GetRequiredService<IOrionClock>();

long start = clock.GetTimestamp();
// ... do work ...
TimeSpan elapsed = clock.GetElapsedTime(start);
```

`AddOrionAbstractions` uses `TryAdd`, so several Orion packages can call it and a clock you register first always wins.

## What is inside

| Namespace | Types | Purpose |
|-----------|-------|---------|
| `Observers` | `SafeObserverInvoker` | Call consumer observer hooks without letting them break the host path. |
| `Diagnostics` | `OrionInstrumentation`, `OrionTelemetry` | One `ActivitySource` + `Meter` per package, static-tag stamping, frozen scope/metric/tag names. |
| `Time` | `IOrionClock`, `SystemOrionClock`, `OrionDeadline` | A clock seam over `TimeProvider` and a monotonic deadline bound to it. |
| `Configuration` | `OrionOptions`, `AddOrionOptions`, `OrionOptionsValidator<T>` | Options that report every validation failure in one message. |
| `Results` | `IOrionResult`, `OrionError`, `OrionErrorCodes` | Expected outcomes as values, with one canonical error-code vocabulary. |

All namespaces live under `Moongazing.Orion.Abstractions`.

## Fault-safe observers

![SafeObserverInvoker decision flow: null observer, success, swallowed fault, propagated cancellation](https://raw.githubusercontent.com/tunahanaliozturk/Orion.Abstractions/main/docs/diagrams/safe-observer-invoke.png)

```csharp
using Moongazing.Orion.Abstractions.Observers;

await SafeObserverInvoker.InvokeAsync(observer,
    o => o.OnSomethingAsync(payload),
    onFault: ex => logger.LogWarning(ex, "observer faulted"),
    cancellationToken: ct);
```

A null observer is a no-op, an observer fault is swallowed and handed to `onFault`, and an `OperationCanceledException` thrown while `ct` is cancelled propagates. `Invoke` is the synchronous form; `Resolve` also runs the observer's resolution inside the guard. `onFault` itself is not guarded and must not throw.

## Options with one failure message

```csharp
using Moongazing.Orion.Abstractions.Configuration;

public sealed class MyOptions : OrionOptions
{
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    public override void Validate(OrionOptionsValidationContext context)
    {
        base.Validate(context);
        context.RequirePositive(Timeout, nameof(Timeout));
    }
}

services.AddOrionOptions<MyOptions>(o => o.Timeout = TimeSpan.FromSeconds(10));
```

The validator is registered once with `TryAddEnumerable`. A rejected configuration throws `OptionsValidationException` on first resolve, listing every failure as `MyOptions.Timeout: must be greater than zero (was 00:00:00).`

## Compatibility

- Targets `net8.0`, `net9.0` and `net10.0`.
- NativeAOT and trim compatible (`IsAotCompatible`), checked by a NativeAOT smoke test in CI.
- Dependencies: `Microsoft.Extensions.DependencyInjection.Abstractions` and `Microsoft.Extensions.Options` only.
- The public surface is frozen across 1.x: source and binary compatible.

## Related packages

- `Orion.Abstractions.Testing` - `FrozenOrionClock`, `RecordingObserver<TObserver>` and `DeterministicFaultInjector` for deterministic tests.

## Links

- Documentation and full README: https://github.com/tunahanaliozturk/Orion.Abstractions
- Conventions every Orion package follows: https://github.com/tunahanaliozturk/Orion.Abstractions/blob/main/docs/CONVENTIONS.md
- Changelog: https://github.com/tunahanaliozturk/Orion.Abstractions/blob/main/CHANGELOG.md
- License: MIT
