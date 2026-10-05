# Orion.Abstractions.Testing

Test doubles for code built on Orion.Abstractions: a frozen, advanceable clock, a recorder for observer calls and faults, and reproducible fault injection. Reference it from test projects only.

![Orion.Abstractions and Orion.Abstractions.Testing: what each package holds and who depends on them](https://raw.githubusercontent.com/tunahanaliozturk/Orion.Abstractions/main/docs/diagrams/overview.png)

## Install

```bash
dotnet add package Orion.Abstractions.Testing
```

It depends on `Orion.Abstractions` and plugs in wherever production code takes an `IOrionClock` or a `SafeObserverInvoker` call site.

## Quick start

```csharp
using Moongazing.Orion.Abstractions.Testing;
using Moongazing.Orion.Abstractions.Time;

var clock = new FrozenOrionClock(); // frozen at 2026-01-01T00:00:00Z unless you pass a start
var deadline = OrionDeadline.After(clock, TimeSpan.FromSeconds(30));

clock.Advance(TimeSpan.FromSeconds(31)); // no real delay

Assert.True(deadline.IsExpired);
```

## FrozenOrionClock

- `Advance(delta)` moves both the wall clock (`UtcNow`) and the monotonic timestamp.
- `SetUtcNow(value)` moves only the wall clock.
- Both reject going backward with `ArgumentOutOfRangeException`, like a real monotonic clock.

## RecordingObserver

Records every observer invocation and every swallowed fault at a `SafeObserverInvoker` call site. Pass `Track` / `TrackAsync` as the action and `OnFault` as the fault hook:

```csharp
using Moongazing.Orion.Abstractions.Observers;
using Moongazing.Orion.Abstractions.Testing;

var recorder = new RecordingObserver<IMyObserver>(myObserver);

SafeObserverInvoker.Invoke(recorder.Observer, recorder.Track(o => o.OnSomething(payload)), recorder.OnFault);

Assert.True(recorder.WasInvoked);
Assert.False(recorder.Faulted);
```

`Invocations`, `Faults`, `SingleFault()` and `Reset()` cover the usual assertions; `Events` is the ordered timeline of invocations and faults, so a test can assert that a fault happened between two invocations.

## DeterministicFaultInjector

Fault injection with no randomness, for retry, backoff and exactly-once tests. Call `Next()` once per attempt (or wrap the operation with `Run` / `RunAsync`); attempts are numbered from 1.

| Schedule | Fails |
|----------|-------|
| `FailFirst(n)` | the first `n` attempts |
| `FailOnAttempts(2, 4)` | exactly those attempts |
| `AlwaysFail()` / `NeverFail()` | every attempt / none |
| `FailUntil(clock, recoversAt)` | while `clock.UtcNow` is before `recoversAt` |
| `When(attempt => ...)` | whenever the predicate returns true |

```csharp
var injector = DeterministicFaultInjector.FailFirst(2);

for (var i = 0; i < 2; i++)
{
    await Assert.ThrowsAsync<DeterministicFaultException>(() => injector.RunAsync(() => CallBackendAsync(ct), ct));
}

var result = await injector.RunAsync(() => CallBackendAsync(ct), ct); // attempt 3 succeeds
```

Injected faults are `DeterministicFaultException` unless you pass a `fault` factory to the schedule.

## Related packages

- `Orion.Abstractions` - the contracts these doubles stand in for.

## Links

- Documentation and full README: https://github.com/tunahanaliozturk/Orion.Abstractions
- Changelog: https://github.com/tunahanaliozturk/Orion.Abstractions/blob/main/CHANGELOG.md
- License: MIT
