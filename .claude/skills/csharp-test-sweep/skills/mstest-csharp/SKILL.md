# MSTest v4

MSTest-specific rules, applied after the [General Quality Checklist](../../references/quality-checklist.md). Assertions are classic style (`Assert.AreEqual(expected, actual)`), not constraint-based. Lookup files: [REFERENCE.md](REFERENCE.md) (assertions, lifecycle, execution control, metadata), [EXAMPLES.md](EXAMPLES.md), [ANTI-PATTERNS.md](ANTI-PATTERNS.md).

## Checklist

- [ ] `[TestClass]` on every test class; `[TestMethod]` on every test method, including data-driven ones (`[DataTestMethod]` is unnecessary since v3)
- [ ] `[DataRow]` for values, `[DynamicData]` for complex or computed data, and for data sets with meaning beyond their values. `[DataRow]` arguments follow the method's parameter order, inputs first and expected values last.
- [ ] `Assert.AreEqual(expected, actual)`: expected first
- [ ] `Assert.ThrowsException<T>` / `Assert.ThrowsExceptionAsync<T>` for exceptions, in place of try/catch + `Assert.Fail`
- [ ] `CollectionAssert` / `StringAssert` in place of `foreach` + `Assert.IsTrue` loops
- [ ] `Assert.Multiple` for independent properties of one scenario
- [ ] `[TestInitialize]` / `[TestCleanup]` for per-test setup, never the constructor. `[ClassInitialize]`, `[ClassCleanup]`, `[AssemblyInitialize]`, `[AssemblyCleanup]` are `static`.
- [ ] `[Ignore]` takes a `// reason` comment with a ticket reference; the attribute stays argument-free
- [ ] `Assert.Inconclusive` links a tracking item and never stands in for a placeholder
- [ ] `[assembly: DiscoverInternals]` in place of making test methods public
- [ ] Async tests return `Task` or `ValueTask`
- [ ] `[Retry]` sparingly
- [ ] `MSTest.Analyzers` referenced
