# Orion.Abstractions.Testing

Testing companion for [Orion.Abstractions](https://www.nuget.org/packages/Orion.Abstractions/).
Ships `FrozenOrionClock`, a deterministic `IOrionClock` whose wall clock and monotonic
timestamp only move when you call `Advance(...)` - so tests of leases, grace periods, and
scheduled work run without real delays or flakiness.

```csharp
var clock = new FrozenOrionClock(start: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
clock.Advance(TimeSpan.FromSeconds(31));
```

MIT.
