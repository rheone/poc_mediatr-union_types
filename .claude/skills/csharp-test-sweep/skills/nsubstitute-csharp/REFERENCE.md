# NSubstitute API Reference

## Creating Substitutes

| Pattern | Description |
|---------|-------------|
| `Substitute.For<T>()` | Single interface or class |
| `Substitute.For<T>(args)` | With constructor arguments |
| `Substitute.For<T1, T2>()` | Implements two interfaces |
| `Substitute.For<T1, T2, T3>()` | Implements three interfaces |
| `Substitute.ForPartsOf<T>()` | Partial — real impl unless configured |
| `Substitute.For([Type]types, args)` | Runtime type array + constructor args |

## Configuring Return Values

| Pattern | Description |
|---------|-------------|
| `sub.Method().Returns(value)` | Stub single return value |
| `sub.Method().Returns(v1, v2)` | Sequence — returns v1, then v2, then v2 onwards |
| `sub.Method().ReturnsNull()` | Returns `null` for nullable return |
| `sub.Method().ReturnsNullForAnyArgs()` | Returns `null` regardless of arguments |
| `sub.Method().ReturnsForAnyArgs(val)` | Stub value regardless of arguments |
| `sub.Method().Returns(Task.FromResult(v))` | Task-returning explicitly |
| `sub.Method().ReturnsAsync(value)` | Wraps value in `Task<T>` |
| `sub.Method().ReturnsAsync(v1, v2)` | Async sequence values |
| `sub.Method().Returns(ci => compute(ci))` | Compute from call info |

## Argument Matchers

| Pattern | Description |
|---------|-------------|
| `Arg.Any<T>()` | Match any value of type `T` |
| `Arg.Is<T>(predicate)` | Match value satisfying `predicate` |
| `Arg.Is<T>(expected)` | Match exact value via `Equals` |
| `Arg.Invoke()` | Invoke the callback argument |
| `Arg.Invoke<T>(value)` | Invoke callback with `value` |
| `Arg.Compat.Ignore<T>()` | Ignore argument (VB.NET compat) |
| `Arg.Do<T>(action)` | Capture argument — see EXAMPLES.md |

## Call Verification

| Pattern | Description |
|---------|-------------|
| `sub.Received().Method()` | Exactly one call |
| `sub.Received(1).Method()` | Exactly one call |
| `sub.Received(2).Method()` | Exactly two calls |
| `sub.ReceivedWithAnyArgs().Method()` | Any args, exactly one call |
| `sub.DidNotReceive().Method()` | Zero calls |
| `sub.DidNotReceiveWithAnyArgs().Method()` | Zero calls, any arguments |
| `sub.ReceivedCalls()` | Return `IEnumerable<ICall>` for inspection |

## Callbacks

| Pattern | Description |
|---------|-------------|
| `sub.When(x => x.Method()).Do(cb)` | Execute callback when method called |
| `sub.When(x => x.Method()).DoNotCallBase()` | Suppress base class virtual |
| `sub.When(x => x.Method()).Throw<TEx>()` | Throw `TException` on call |
| `sub.When(x => x.Method()).Throw(ex)` | Throw specific exception instance |
| `Returns(ci => { ...; return val; })` | Inline callback in Returns |

## Property Behavior

| Feature | Description |
|---------|-------------|
| Auto-properties | Virtual get/set work automatically |
| `sub.Prop.Returns(value)` | Explicit property stub |
| `sub.Prop.Returns(v1, v2)` | Sequence property values |

## Clearing & Resetting

| Pattern | Description |
|---------|-------------|
| `sub.ClearReceivedCalls()` | Reset call history only |
| `sub.ClearSubstitute()` | Reset calls AND configured returns |
