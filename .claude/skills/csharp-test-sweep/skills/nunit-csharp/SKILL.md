# NUnit v5

NUnit-specific rules, applied after the [General Quality Checklist](../../references/quality-checklist.md). Lookup files: [REFERENCE.md](REFERENCE.md) (constraint catalog, lifecycle, parallelization, data patterns), [EXAMPLES.md](EXAMPLES.md), [ANTI-PATTERNS.md](ANTI-PATTERNS.md) (numbered; cited below).

## Checklist

- [ ] Constraint-based `Assert.That(actual, Is.EqualTo(expected))` throughout; classic `Assert.AreEqual` and `StringAssert` are replaced (`Does.Contain`), so one style holds project-wide (#1, #2)
- [ ] Multi-condition checks compose constraints (`Is.GreaterThan(0) & Is.LessThan(100)`) in place of chained `Assert.That`
- [ ] `Assert.Multiple` for independent properties of one scenario, so every failure is reported (#5):

  ```csharp
  Assert.Multiple(() =>
  {
      Assert.That(result.IsSuccess, Is.True);
      Assert.That(result.Value, Is.EqualTo(42));
  });
  ```

- [ ] `[SetUp]` for per-test setup, never the constructor: NUnit creates one instance per test (#3)
- [ ] `[Test]` for one scenario; `[TestCase]` for simple literals, `[TestCaseSource(nameof(...))]` for complex or computed data, `TestCaseData.SetName()` for readable display names
- [ ] Exceptions: `Assert.That(() => ..., Throws.InstanceOf<T>())` (async lambdas for async code), or `Assert.Throws<T>` followed by a check on the exception (#7)
- [ ] `[Ignore("...")]` carries a reason and ticket reference (`[Ignore("Blocked by #42 — IPv6 parsing")]`)
- [ ] `[Parallelizable(ParallelScope.Children)]` expresses the intent in place of per-test `[NonParallelizable]`
- [ ] `[Retry]` only for unreliable infrastructure interaction, never to mask non-deterministic logic
- [ ] `[TestFixture]` only where required: a base class, constructor injection, or `[TestFixtureSource]`
- [ ] No static mutable state in `[OneTimeSetUp]` (#6)
- [ ] `NUnit.Analyzers` referenced

## Private and internal members

Public API first; `[assembly: InternalsVisibleTo("Tests")]` for `internal`; reflection last.
