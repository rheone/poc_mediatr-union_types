# Telerik JustMock

JustMock-specific rules, applied after the [General Quality Checklist](../../references/quality-checklist.md). Lookup files: [REFERENCE.md](REFERENCE.md) (API tables), [EXAMPLES.md](EXAMPLES.md), [ANTI-PATTERNS.md](ANTI-PATTERNS.md) (numbered; cited below). JustMock ships no Roslyn analyzer; build-time tests catch Free-mode runtime failures.

## Free vs Elevated

The API is identical in both modes; what differs is what can be intercepted.

| Mode | License | Mechanism | Can mock |
|------|---------|-----------|----------|
| **Free** | None | Dynamic proxy (Castle.Core) | Interfaces, abstract and virtual members |
| **Elevated** | Commercial Telerik license + profiler | CLR instrumentation | Everything: non-virtual, static, sealed, private, `DateTime.Now` |

Free mode has the same limits as Moq or NSubstitute. Examples in this skill are Free mode unless marked `[Elevated]`.

## Checklist

- [ ] `Mock.Create<T>()` for dependencies only. When the method under test lives on an abstract base class, instantiate a concrete subclass. Elevated mode intercepts even non-virtual members, so verify against expected results rather than call counts (#1, #2).
- [ ] `Mock.Assert` with explicit `Occurs` sits in the Assert section; every `Mock.Arrange` that matters has one (#5)
- [ ] Free mode constraints (virtual-only) respected; Elevated features only where the license and profiler are confirmed, and only when Free cannot do the job (#3, #4, #7)
- [ ] `Arg.Matches<T>(predicate)` whenever the value is knowable at write time; `Arg.IsAny<T>()` for a `CancellationToken` not under test
- [ ] `Behavior.Loose` plus targeted `Mock.Assert`; `Strict` sparingly (#6)
- [ ] `Mock.CreateLike<T>()` in place of per-property arrangements when defaults suffice
- [ ] `DoInstead` captures arguments; the captured values are asserted in the Assert section
