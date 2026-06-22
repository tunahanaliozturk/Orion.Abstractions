# The Orion observer contract

This is the normative contract for an **Orion observer**: any consumer-supplied hook that an
Orion library invokes through
[`SafeObserverInvoker`](../src/Moongazing.Orion.Abstractions/Observers/SafeObserverInvoker.cs).
Examples across the family include dead-letter sinks, lock-event observers, and
encryption-audit observers.

The contract exists because an observer is **observability, not load-bearing logic**. The host
path an observer is attached to (delivering a message, acquiring a lock, encrypting a payload)
must not fail, stall, or change behavior because of what the observer does. `SafeObserverInvoker`
enforces the host-protecting half of this mechanically; the rules below are the half an observer
author is responsible for.

The key words MUST, MUST NOT, SHOULD, SHOULD NOT, and MAY are used in the sense of
[RFC 2119](https://www.rfc-editor.org/rfc/rfc2119).

## What `SafeObserverInvoker` guarantees to the host

These are facts about the invoker, not requirements on you. They are what makes the rules in the
next section safe to rely on.

- **A null observer is a no-op.** If the observer (or, for `Resolve`, the factory result) is
  null, the call site is skipped and nothing runs. Null means "no observer is attached", never
  an error.
- **An observer fault is swallowed.** Any exception thrown by the observer action is caught and
  does not propagate into the host path. If an `onFault` callback was supplied, the swallowed
  exception is passed to it; otherwise it is dropped. Either way the host continues.
- **Resolution faults are swallowed too.** `SafeObserverInvoker.Resolve` runs the observer
  factory *inside* the same guard, so an observer whose construction or dependency injection
  throws cannot abort the host path at resolution time.
- **Cancellation is never downgraded.** In `InvokeAsync`, an `OperationCanceledException` is
  re-thrown (not swallowed) when, and only when, the cancellation token supplied to the invoker
  has actually been cancelled. Cooperative shutdown therefore reaches the host as a cancellation,
  not as a silently swallowed warning. An `OperationCanceledException` that is *not* backed by a
  cancelled token is treated as an ordinary fault and swallowed.
- **A programming-error argument still throws.** A null `action` (or a null `resolve` factory)
  is rejected with `ArgumentNullException`. That is a defect in the calling code, not an observer
  fault, so it is not swallowed.

## Rules for an observer

### 1. An observer MUST NOT rely on its exceptions reaching the host

A fault thrown from the observer action is swallowed by design. Do not use throwing as a control
signal back to the host, and do not assume a retry, a rollback, or any host-visible effect will
result from throwing. If the observer needs durability or retries, it MUST own that itself (for
example, by writing to its own queue), because the host treats the observer as fire-and-forget.

### 2. An `onFault` callback MUST NOT throw

The `onFault` hook is the last line of fault handling. It runs *after* an observer fault has
already been swallowed, and the invoker does not guard it a second time. A throwing `onFault`
turns a swallowed, harmless observer fault into an exception on the host path, defeating the
entire purpose of the guard. Keep `onFault` to the cheapest possible logging or metric increment,
and make it total: it MUST handle every exception type it can be handed (including ones it did not
anticipate) without throwing.

### 3. An observer MUST NOT block the host

The synchronous `Invoke` and `Resolve` entry points run the observer action *inline* on the host
thread. The host does not move on until the action returns. An observer therefore MUST NOT perform
slow or unbounded work on the calling thread: no synchronous network or disk I/O, no lock
acquisition that can wait indefinitely, no `Task.Wait()` / `.Result` / `.GetAwaiter().GetResult()`
sync-over-async. Such work stalls the load-bearing path just as surely as throwing would break it.
Do the minimum inline (capture a value, enqueue, increment a counter) and offload anything heavier.

### 4. An async observer MUST honor the cancellation token

For `InvokeAsync`, the cancellation token passed to the invoker is the host's shutdown signal.
An async observer SHOULD flow that token into any awaited call it makes and SHOULD stop promptly
when it is cancelled. When the observer stops because that token was cancelled, it MUST surface an
`OperationCanceledException` (which the invoker re-throws to the host) rather than catching it and
returning normally. Swallowing cancellation inside the observer hides shutdown from the host.

### 5. An async observer SHOULD NOT throw `OperationCanceledException` for non-cancellation reasons

The invoker distinguishes a real cancellation (token is cancelled) from an
`OperationCanceledException` raised for any other reason: only the former propagates, and the
latter is swallowed as an ordinary fault. An observer SHOULD NOT raise
`OperationCanceledException` to signal an ordinary error, because doing so muddies that
distinction. Use an exception type that reflects the actual failure.

### 6. An observer SHOULD be null rather than empty

Because a null observer is a genuine no-op (the action never runs and nothing is allocated for
it), "no observer attached" SHOULD be represented as null rather than as an empty or do-nothing
observer instance. This keeps the cheapest path cheap.

### 7. An observer SHOULD NOT depend on ordering or threading guarantees beyond its own call

`SafeObserverInvoker` makes no promise about concurrency between separate invocations: the host
MAY invoke the same observer from multiple threads, and MAY invoke several observers in any order.
An observer that keeps state across invocations is responsible for its own thread safety. The
invoker guards each call against faults; it does not serialize calls.

## Verifying an observer against this contract

The `Orion.Abstractions.Testing` package ships
[`RecordingObserver<TObserver>`](../src/Moongazing.Orion.Abstractions.Testing/RecordingObserver.cs),
a recording double for `SafeObserverInvoker` call sites. Pass its `Track` / `TrackAsync` wrapper
as the action and its `OnFault` as the fault hook, then assert on what was recorded:

```csharp
using Moongazing.Orion.Abstractions.Observers;
using Moongazing.Orion.Abstractions.Testing;

var recorder = new RecordingObserver<IMyObserver>(myObserver);

SafeObserverInvoker.Invoke(
    recorder.Observer,
    recorder.Track(o => o.OnSomething(payload)),
    recorder.OnFault);

Assert.True(recorder.WasInvoked);   // the action ran to completion
Assert.False(recorder.Faulted);     // no fault was swallowed
```

It records a swallowed fault (rule 1 / rule 2) without an accompanying invocation, and it does
*not* record a propagated cancellation as a fault (rule 4), so it can assert each rule above
directly. See `RecordingObserverTests` for worked examples of every path.
