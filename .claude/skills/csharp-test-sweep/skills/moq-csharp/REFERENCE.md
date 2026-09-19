# Moq 4.x API Reference

## Mock Creation

| Task | Code |
|------|------|
| Interface mock | `new Mock<IFoo>()` |
| Class mock (default ctor) | `new Mock<Foo>()` |
| Class mock (custom ctor args) | `new Mock<Foo>(arg1, arg2)` |
| LINQ-to-mocks | `Mock.Of<IFoo>(x => x.Name == "a")` |
| Retrieve mock from object | `Mock.Get(instance)` |
| Strict mock | `new Mock<IFoo>(MockBehavior.Strict)` |

## Setup

| Task | Code |
|------|------|
| Method return value | `mock.Setup(x => x.Method(args)).Returns(v)` |
| Async method return | `mock.Setup(x => x.TaskMethod(args)).ReturnsAsync(v)` |
| Throws exception | `mock.Setup(x => x.Method(args)).Throws<TEx>()` |
| Throws async exception | `mock.Setup(x => x.TaskMethod(args)).ThrowsAsync<TEx>()` |
| Property getter | `mock.SetupGet(x => x.Prop).Returns(v)` |
| Property setter | `mock.SetupSet(x => x.Prop = It.IsAny<T>())` |
| Auto-stub property | `mock.SetupProperty(x => x.Prop, defaultValue)` |
| Sequential returns | `mock.SetupSequence(x => x.Get()).Returns("a").Returns("b")` |
| Call base impl | `mock.Setup(x => x.VirtualMethod()).CallBase()` |

## Returns / Throws Variants

| Return type | Method |
|------------|--------|
| `TResult` | `.Returns(value)` |
| `Task<TResult>` | `.ReturnsAsync(value)` |
| `ValueTask<TResult>` | `.ReturnsAsync(value)` |
| `Task` | `.Returns(Task.CompletedTask)` |
| Void | `.Callback(...)` / no `.Returns()` |
| Exception | `.Throws<TException>()` |
| Async exception | `.ThrowsAsync<TException>()` |
| Compute at call time | `.Returns(() => ComputeValue())` |

## Argument Matchers

| Matcher | Example |
|---------|---------|
| `It.IsAny<T>()` | `x => x.Method(It.IsAny<string>())` |
| `It.Is<T>(pred)` | `x => x.Method(It.Is<int>(i => i > 0))` |
| `It.IsInRange<T>(lo, hi, Range.Inclusive)` | `x => x.Method(It.IsInRange(1, 10, Range.Inclusive))` |
| `It.IsRegex(pattern)` | `x => x.Method(It.IsRegex(@"^\d{3}-\d{4}$"))` |
| `It.IsNotNull<T>()` | `x => x.Method(It.IsNotNull<string>())` |

## Verification

| Task | Code |
|------|------|
| Called exactly once | `mock.Verify(x => x.Method(a), Times.Once())` |
| Never called | `mock.Verify(x => x.Method(a), Times.Never())` |
| Called N times | `mock.Verify(x => x.Method(a), Times.Exactly(3))` |
| Verify all set up calls | `mock.VerifyAll()` |
| Property get called | `mock.VerifyGet(x => x.Prop, Times.Once())` |
| Property set called | `mock.VerifySet(x => x.Prop = v, Times.Once())` |
| With failure message | `mock.Verify(x => x.Method(a), "message")` |
| Mark setup as verifiable | `.Verifiable()` then `mock.VerifyAll()` |

## Times Members

| Member | Meaning |
|--------|---------|
| `Times.Once()` | 1 |
| `Times.Never()` | 0 |
| `Times.Exactly(n)` | `n` |
| `Times.AtLeastOnce()` | ≥ 1 |
| `Times.AtLeast(n)` | ≥ `n` |
| `Times.AtMost(n)` | ≤ `n` |
| `Times.Between(a, b, Range)` | Between `a` and `b` |

## Callbacks

| Pattern | Use |
|---------|-----|
| `.Callback(() => action)` | No-arg void methods |
| `.Callback<T>(x => action)` | Capture single typed argument |
| `.Callback((a, b) => action)` | Multi-arg capture |
| `.Callback(() => count++)` | Side-effect on call |

## Capture

| Pattern | Use |
|---------|-----|
| `Capture.In(list)` | Append each argument to a `List<T>` |
| `Capture.Match<T>(pred)` | Match-and-capture with predicate |

## Properties

| Pattern | Effect |
|---------|--------|
| `.SetupProperty(x => x.Prop)` | Get/set stub — acts like auto-property |
| `.SetupProperty(x => x.Prop, "initial")` | Get/set with initial value |
| `.SetupGet(x => x.Prop).Returns(v)` | Read-only property stub |
