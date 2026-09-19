# RhinoMocks

RhinoMocks has been unmaintained since 2020. This companion keeps legacy suites healthy; every touched file is flagged for migration to [NSubstitute](https://nsubstitute.github.io/) or [Moq](https://github.com/devlooped/moq) in the sweep summary.

Rules apply after the [General Quality Checklist](../../references/quality-checklist.md). Lookup files: [REFERENCE.md](REFERENCE.md), [EXAMPLES.md](EXAMPLES.md), [ANTI-PATTERNS.md](ANTI-PATTERNS.md) (numbered; cited below). No Roslyn analyzer exists for RhinoMocks, so the sweep review is the only safeguard (#7).

## Checklist

- [ ] AAA style: `GenerateMock` / `Stub` / `AssertWasCalled`. New tests never use record/replay (`Expect` / `ReplayAll` / `VerifyAll`); existing record/replay tests are flagged for migration to AAA (#1, #4, #6).
- [ ] `GenerateMock<T>` / `GenerateStub<T>` for dependencies only. When the method under test lives on an abstract base class, instantiate a concrete subclass (#2).
- [ ] `AssertWasCalled` / `AssertWasNotCalled` sit in the Assert section; every stub that matters has an `AssertWasCalled`
- [ ] `Arg<T>.Is.Equal(v)` whenever the value is knowable; `Arg<T>.Is.Anything` otherwise
- [ ] `GeneratePartialMock<T>` replaced by a concrete subclass
- [ ] `RhinoAutoMocker<T>` (StructureMap.AutoMocking) only where its hidden dependency setup stays readable; explicit mocks when setup is non-trivial (#5)

```csharp
var mock = MockRepository.GenerateMock<IDependency>();
mock.Stub(x => x.GetValue()).Return(42);
var sut = new Service(mock);
var result = sut.DoWork();
Assert.Equal(42, result);
mock.AssertWasCalled(x => x.GetValue());
```

## Migration

| RhinoMocks                         | NSubstitute               | Moq                                 |
| ---------------------------------- | ------------------------- | ----------------------------------- |
| `GenerateMock<T>()`                | `Substitute.For<T>()`     | `new Mock<T>()`                     |
| `stub.Stub(x => x.M()).Return(v)`  | `sub.Method().Returns(v)` | `mock.Setup(x => x.M()).Returns(v)` |
| `mock.AssertWasCalled(x => x.M())` | `sub.Received().Method()` | `mock.Verify(x => x.M())`           |
| `Arg<T>.Is.Equal(v)`               | `Arg.Is<T>(x => x == v)`  | `It.Is<T>(x => x == v)`             |
