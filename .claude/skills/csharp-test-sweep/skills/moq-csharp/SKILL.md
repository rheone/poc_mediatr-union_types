# Moq

Moq-specific rules, applied after the [General Quality Checklist](../../references/quality-checklist.md). Lookup files: [REFERENCE.md](REFERENCE.md) (API tables), [EXAMPLES.md](EXAMPLES.md), [ANTI-PATTERNS.md](ANTI-PATTERNS.md) (numbered; cited below).

## Checklist

- [ ] `new Mock<T>()` for dependencies only. When the method under test lives on an abstract base class, instantiate a concrete subclass; `.CallBase()` on the subject is a symptom (#1, #2).
- [ ] `mock.Object` only when injecting into the subject
- [ ] Interfaces preferred; a concrete class gets explicit constructor arguments (#9)
- [ ] `mock.Verify` sits in the Assert section; every setup that matters has a `Verify`, and a setup that is never verified is removed (#8)
- [ ] `Times.Once()` over `AtLeastOnce()` unless the count is genuinely variable; `VerifyAll` is never the default (#3)
- [ ] `It.Is<T>(predicate)` whenever the value is knowable at write time; `It.IsAny<T>()` for a `CancellationToken` not under test
- [ ] `MockBehavior.Loose` plus targeted `Verify`; `Strict` only where every call must be intentional (#4, #6)
- [ ] `Callback` captures arguments; the captured values are asserted in the Assert section (#7)
- [ ] `Mock.Of<T>` for simple read-only arrangements; `new Mock<T>()` once verification, callbacks, or sequences are needed
- [ ] `Moq.Analyzers` referenced
