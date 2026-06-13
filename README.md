# Orion.Abstractions

[![CI/CD](https://github.com/tunahanaliozturk/Orion.Abstractions/actions/workflows/ci-cd.yml/badge.svg)](https://github.com/tunahanaliozturk/Orion.Abstractions/actions/workflows/ci-cd.yml)
[![NuGet](https://img.shields.io/nuget/v/Orion.Abstractions.svg)](https://www.nuget.org/packages/Orion.Abstractions/)

Shared foundation primitives for the **Orion** family of .NET libraries. This package has
no Orion dependencies of its own; any library can depend on it to inherit the Orion
conventions, and the existing family members (OrionGuard, OrionPatch, OrionAudit, OrionLock,
OrionKey, OrionVault) converge on it over time.

It exists because three primitives kept being re-implemented (and kept drifting) across the
family. They now live here, once, correctly.

## What's inside

### 1. Fault-safe observer invocation (`SafeObserverInvoker`)

Every Orion package exposes consumer observer hooks (`IDeadLetterSink`, `ILockEventObserver`,
`IEncryptionAuditObserver`, ...). They all follow the same contract: a null observer is a
no-op, observer faults are swallowed (observability must never break the load-bearing path),
and `OperationCanceledException` always propagates on cancellation.

```csharp
SafeObserverInvoker.Invoke(observer, o => o.OnSomething(payload),
    onFault: ex => logger.LogWarning(ex, "observer faulted; host continued"));

await SafeObserverInvoker.InvokeAsync(observer,
    o => o.OnSomethingAsync(payload, ct), ct,
    onFault: ex => logger.LogWarning(ex, "observer faulted"));

// Resolution itself inside the guard - a throwing observer ctor cannot abort the host path.
SafeObserverInvoker.Resolve(() => sp.GetService<IMyObserver>(), o => o.OnSomething(payload));
```

### 2. OpenTelemetry conventions (`OrionInstrumentation`)

A base class that creates a consistently-named `ActivitySource` + `Meter` and provides the
"static tags" pattern (set once at startup, stamped onto every measurement) for multi-tenant
/ multi-region dashboard splitting without a second Meter.

```csharp
public sealed class MyDiagnostics : OrionInstrumentation
{
    public MyDiagnostics() : base("Moongazing.MyPackage", "1.0.0") { }
    public readonly Counter<long> Things = /* Meter.CreateCounter... */;
}

diag.SetStaticTags(new Dictionary<string,string> { ["tenant"] = tenantId });
things.Add(1, diag.Tag(new("outcome", "ok")));   // appends the static tags
```

### 3. Testable clock (`IOrionClock` / `SystemOrionClock` + `FrozenOrionClock`)

A thin seam over `TimeProvider` so every Orion background worker, lease, and scheduler shares
one clock contract and one DI registration. `Orion.Abstractions.Testing` ships
`FrozenOrionClock` for deterministic tests of lease expiry, grace periods, and scheduled work.

```csharp
services.AddOrionAbstractions();   // registers IOrionClock -> SystemOrionClock (TryAdd)

// in tests:
var clock = new FrozenOrionClock();
clock.Advance(TimeSpan.FromSeconds(31));   // drive a lease past expiry, no real delay
```

## Packages

| Package | Purpose |
|---------|---------|
| `Orion.Abstractions` | The shared primitives above. |
| `Orion.Abstractions.Testing` | `FrozenOrionClock` and future test doubles. |

## Design

- Multi-targets `net8.0`, `net9.0`, `net10.0`.
- `TreatWarningsAsErrors`, latest analyzers, nullable enabled.
- No dependencies beyond `Microsoft.Extensions.DependencyInjection.Abstractions`.

## License

MIT.
