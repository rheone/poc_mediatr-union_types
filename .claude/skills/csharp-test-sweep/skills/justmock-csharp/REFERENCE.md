# JustMock API Reference

## Mock Creation

| Scenario | Code | Mode |
|----------|------|------|
| Interface | `var mock = Mock.Create<IFoo>();` | Free + Elevated |
| Abstract class | `var mock = Mock.Create<AbstractBase>();` | Free + Elevated |
| Concrete class (virtual members) | `var mock = Mock.Create<Foo>();` | Free + Elevated |
| Concrete class (constructor args) | `var mock = Mock.Create<Foo>(arg1, arg2);` | Free + Elevated |
| Concrete class (non-virtual members) | `var mock = Mock.Create<Foo>();` | Elevated only |
| Sealed class | `var mock = Mock.Create<SealedFoo>();` | Elevated only |
| Strict mode | `var mock = Mock.Create<IFoo>(Behavior.Strict);` | Free + Elevated |
| Auto-stub properties | `var mock = Mock.CreateLike<IFoo>();` | Free + Elevated |

## Arrange — Return Values

```csharp
Mock.Arrange(() => mock.Method("arg")).Returns("result");
Mock.Arrange(() => mock.Method(Arg.IsAny<string>())).Returns("default");
Mock.Arrange(() => mock.Property).Returns("value");
```

## Arrange — Exceptions

```csharp
Mock.Arrange(() => mock.Method("bad")).Throws<InvalidOperationException>();
Mock.Arrange(() => mock.Method("bad")).Throws(new InvalidOperationException("msg"));
```

## Arrange — Async (TaskResult)

```csharp
// Free mode: async methods must be virtual
Mock.Arrange(() => mock.FetchAsync(Arg.IsAny<int>()))
    .TaskResult(new User { Id = 1 });
```

## Arrange — Callbacks

```csharp
Mock.Arrange(() => mock.Method(Arg.IsAny<string>()))
    .DoInstead((string s) => captured = s);

Mock.Arrange(() => mock.Method())
    .DoInstead(() => callCount++);
```

## Arrange — Sequences

```csharp
Mock.Arrange(() => mock.GetStatus())
    .Returns("Pending")
    .ReturnsOnce("Approved")
    .ReturnsOnce("Completed");
```

## Verify — Mock.Assert

```csharp
Mock.Assert(() => mock.Method("arg"), Occurs.Once());
Mock.Assert(() => mock.Method(Arg.IsAny<string>()), Occurs.Never());
Mock.Assert(() => mock.Method("arg"), Occurs.Exactly(3));
Mock.Assert(() => mock.Method("arg"), Occurs.AtLeast(1));
Mock.Assert(() => mock.Method("arg"), Occurs.AtMost(5));
```

## Occurs Members

| Expression | Meaning |
|------------|---------|
| `Occurs.Once()` | Exactly 1 call |
| `Occurs.Never()` | Exactly 0 calls |
| `Occurs.Exactly(n)` | Exactly `n` calls |
| `Occurs.AtLeast(n)` | `n` or more |
| `Occurs.AtMost(n)` | `n` or fewer |

## Argument Matchers

| Expression | Meaning |
|------------|---------|
| `Arg.IsAny<T>()` | Any value of type `T` |
| `Arg.Matches<T>(pred => pred)` | Value matching predicate |
| `Arg.IsInRange<T>(lo, hi, Range.Inclusive)` | Range inclusive |
| `Arg.IsInRange<T>(lo, hi, Range.Exclusive)` | Range exclusive |
| `Arg.AnyString` | Any `string` value |
| `Arg.AnyInt` | Any `int` value |

## Behavior Modes

| Mode | Behavior | Use |
|------|----------|-----|
| `Behavior.Loose` (default) | Returns type defaults for unarranged members | Most tests — arrange only what matters |
| `Behavior.Strict` | Throws `MockException` on unarranged calls | Interaction testing — every call must be intentional |

## Mock.CreateLike

Auto-initializes all properties to their type defaults (empty strings, zeros, empty collections). No property arrangement needed:

```csharp
var mock = Mock.CreateLike<IFoo>();
// mock.Name == "", mock.Age == 0, mock.Tags == null
```

## Mock.AssertMultiple

Batch multiple assertions — reports all failures together:

```csharp
Mock.AssertMultiple(() =>
{
    Mock.Assert(() => mock.Save(c), Occurs.Once());
    Mock.Assert(() => mock.Log("saved"), Occurs.Once());
});
```

## Elevated-Only Features

These compile in Free mode but **throw at runtime**:

| Feature | Code |
|---------|------|
| Mock sealed class | `Mock.Create<SealedClass>()` |
| Mock static method | `Mock.Arrange(() => StaticClass.Method()).Returns(x);` |
| Mock static class | `Mock.SetupStatic<StaticService>();` |
| Intercept DateTime | `Mock.Arrange(() => DateTime.Now).Returns(fixedDate);` |
| Intercept non-virtual method | `Mock.Arrange(() => mock.NonVirtualMethod()).Returns(x);` |
| Mock private method | `Mock.NonPublic.Arrange<ReturnType>(mock, "PrivateMethod", args).Returns(x);` |
| Mock constructor | `Mock.Arrange(() => new Foo(Arg.IsAny<int>())).IgnoreInstance();` |
| SetupAllProperties | `Mock.SetupAllProperties(mock);` — auto-implements property setter tracking |
