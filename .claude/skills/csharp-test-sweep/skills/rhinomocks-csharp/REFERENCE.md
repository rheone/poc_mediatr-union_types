# RhinoMocks API Reference

## Mock Creation

| API | Purpose | Notes |
| --- | ------- | ----- |
| `MockRepository.GenerateMock<T>()` | Creates a mock that tracks expectations and verifies calls | Preferred for interaction testing |
| `MockRepository.GenerateStub<T>()` | Creates a stub that auto-implements properties but does not track calls | Use when only return values are needed |
| `MockRepository.GeneratePartialMock<T>()` | Creates a mock that runs real implementation unless a method is stubbed | Use for testing virtual methods on concrete classes |

## Stub (AAA Style)

```csharp
mock.Stub(x => x.Method()).Return(value);
mock.Stub(x => x.Method()).Throw<TException>();
mock.Stub(x => x.Method(args)).Return(value);
mock.Stub(x => x.Property).Return(value);
```

## Expect (Record/Replay — Deprecated)

```csharp
mock.Expect(x => x.Method()).Return(value);
mock.Expect(x => x.Method()).Throw<TException>();
repository.ReplayAll();   // switch from record to replay
// ... act ...
repository.VerifyAll();   // verify all expectations met
```

## Verification (AAA Style)

| API | Purpose |
| --- | ------- |
| `mock.AssertWasCalled(x => x.Method())` | Asserts method was called at least once |
| `mock.AssertWasCalled(x => x.Method(), o => o.Repeat.Once())` | Asserts method was called exactly once |
| `mock.AssertWasNotCalled(x => x.Method())` | Asserts method was never called |
| `mock.AssertWasCalled(x => x.Method(Arg<T>.Is.Anything))` | Asserts method called with any argument |

## Argument Matchers

| Matcher | Example |
| ------- | ------- |
| `Arg<T>.Is.Equal(value)` | `Arg<int>.Is.Equal(42)` |
| `Arg<T>.Is.Anything` | `Arg<string>.Is.Anything` |
| `Arg<T>.Is.NotNull` | `Arg<Stream>.Is.NotNull` |
| `Arg<T>.Is.Null` | `Arg<string>.Is.Null` |
| `Arg<T>.Match(predicate)` | `Arg<int>.Match(x => x > 0 && x < 100)` |
| `Arg.Text.StartsWith(str)` | `Arg.Text.StartsWith("prefix")` |
| `Arg.Text.EndsWith(str)` | `Arg.Text.EndsWith("suffix")` |
| `Arg.Text.Contains(str)` | `Arg.Text.Contains("substring")` |
| `Arg.Text.Like(regex)` | `Arg.Text.Like("^foo.*bar$")` |
| `List.ArgList<T>(params T[])` | `List.ArgList(1, 2, 3)` |

## Repeat Constraints

| Constraint | Purpose |
| ---------- | ------- |
| `.Repeat.Once()` | Exactly one call |
| `.Repeat.Twice()` | Exactly two calls |
| `.Repeat.Any()` | Zero or more calls (default) |
| `.Repeat.Times(N)` | Exactly N calls |
| `.Repeat.AtLeastOnce()` | One or more calls |

Apply to both Stub and Expect:
```csharp
mock.Stub(x => x.Method()).Return(1).Repeat.Twice();
mock.Expect(x => x.Method()).Return(2).Repeat.Any();
mock.AssertWasCalled(x => x.Method(), o => o.Repeat.Once());
```

## Property Behavior

`GenerateStub<T>()` automatically implements property getters/setters on interfaces:
```csharp
var stub = MockRepository.GenerateStub<IConfig>();
stub.Name = "test";       // auto-implements get/set
var value = stub.Name;    // "test"
```

For mocks, use `.Stub()`:
```csharp
var mock = MockRepository.GenerateMock<IConfig>();
mock.Stub(x => x.Name).Return("test");
```

## StructureMap.AutoMocking (RhinoAutoMocker)

| API | Purpose |
| --- | ------- |
| `new RhinoAutoMocker<T>(MockMode.AAA)` | Creates container with auto-mocked dependencies |
| `autoMocker.Get<TDependency>()` | Retrieves the mock/stub for a dependency |
| `autoMocker.ClassUnderTest` | The SUT with all dependencies injected |
| `autoMocker.PartialMockTheClassUnderTest` | Use `GeneratePartialMock` for the SUT |

```csharp
var autoMocker = new RhinoAutoMocker<Service>(MockMode.AAA);
autoMocker.Get<ILogger>().Stub(x => x.Log(Arg<string>.Is.Anything));
autoMocker.Get<IRepository>().Stub(x => x.Find(1)).Return(entity);
var result = autoMocker.ClassUnderTest.Execute();
```
