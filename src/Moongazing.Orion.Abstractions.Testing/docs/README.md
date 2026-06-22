# Orion.Abstractions.Testing

Testing companion for [Orion.Abstractions](https://www.nuget.org/packages/Orion.Abstractions/).

`FrozenOrionClock` is a deterministic `IOrionClock` whose wall clock and monotonic timestamp
only move when you call `Advance(...)` - so tests of leases, grace periods, and scheduled work
run without real delays or flakiness.

```csharp
var clock = new FrozenOrionClock(start: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
clock.Advance(TimeSpan.FromSeconds(31));
```

`RecordingObserver<TObserver>` records every observer invocation and every swallowed fault at a
`SafeObserverInvoker` call site, so you can assert your observers honor the Orion observer
contract. Pass its `Track` / `TrackAsync` wrapper as the action and its `OnFault` as the fault
hook.

```csharp
var recorder = new RecordingObserver<IMyObserver>(myObserver);
SafeObserverInvoker.Invoke(recorder.Observer, recorder.Track(o => o.OnSomething(payload)), recorder.OnFault);
Assert.True(recorder.WasInvoked);
Assert.False(recorder.Faulted);
```

MIT.
