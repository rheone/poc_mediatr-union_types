# NSubstitute

NSubstitute-specific rules, applied after the [General Quality Checklist](../../references/quality-checklist.md). Lookup files: [REFERENCE.md](REFERENCE.md), [EXAMPLES.md](EXAMPLES.md), [ANTI-PATTERNS.md](ANTI-PATTERNS.md) (numbered; cited below).

## Checklist

- [ ] `Substitute.For<T>()` for dependencies only. When the method under test lives on an abstract base class, instantiate a concrete subclass (#1).
- [ ] `Received` / `DidNotReceive` sit in the Assert section; every stub that matters has a `Received` check, since `Returns` without one is dead configuration (#3)
- [ ] `Received(1)` in place of bare `Received()`; `DidNotReceive()` asserts absence
- [ ] `Arg.Is<T>(predicate)` whenever the value is knowable at write time. `Arg.Any<T>()` is for a `CancellationToken` not under test, complex objects verified by a separate assertion, and call-count or ordering tests (#2).
- [ ] `.Returns(x => ...)` for computed returns; `.When(...).Do(...)` for side effects
- [ ] `Substitute.ForPartsOf<T>()` sparingly: it signals a dependency worth extracting (#4)
- [ ] Non-virtual members are never stubbed (#7); substitutes are fresh per test (#6)
- [ ] `NSubstitute.Analyzers` referenced

## Abstract classes

`Substitute.For<AbstractClass>()` is correct when the class is a **dependency** injected into the subject, the test verifies interactions, and the member is abstract (no body to intercept):

```csharp
var dependency = Substitute.For<AbstractProcessor>();
dependency.Process(Arg.Any<string>()).Returns("ok");

var sut = new MyService(dependency);
sut.Run("input");

dependency.Received(1).Process("input");
```
