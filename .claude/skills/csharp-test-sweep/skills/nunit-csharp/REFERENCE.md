# NUnit v5 Reference

## Constraint Catalog

Use `Assert.That(actual, constraint)` — never classic `Assert.AreEqual`.

### Equality & Identity

| Constraint | Verifies |
| ---------- | -------- |
| `Is.EqualTo(expected)` | Value equality |
| `Is.Not.EqualTo(expected)` | Value inequality |
| `Is.SameAs(expected)` | Reference identity |
| `Is.Not.SameAs(expected)` | Different reference |

### Type Checking

| Constraint | Verifies |
| ---------- | -------- |
| `Is.InstanceOf<T>()` | `actual is T` |
| `Is.AssignableFrom<T>()` | `typeof(T).IsAssignableFrom(actual.GetType())` |

### Comparisons

| Constraint | Verifies |
| ---------- | -------- |
| `Is.GreaterThan(n)` | `actual > n` |
| `Is.GreaterThanOrEqualTo(n)` | `actual >= n` |
| `Is.LessThan(n)` | `actual < n` |
| `Is.LessThanOrEqualTo(n)` | `actual <= n` |
| `Is.InRange(lo, hi)` | `lo <= actual <= hi` |

### Null & Boolean

| Constraint | Verifies |
| ---------- | -------- |
| `Is.Null` | `actual is null` |
| `Is.Not.Null` | `actual is not null` |
| `Is.True` | `actual == true` |
| `Is.False` | `actual == false` |

### Strings

| Constraint | Verifies |
| ---------- | -------- |
| `Does.Contain("sub")` | Contains substring |
| `Does.StartWith("pre")` | Starts with prefix |
| `Does.EndWith("suf")` | Ends with suffix |
| `Does.Match(@"regex")` | Regex match |

Use `.IgnoreCase` for case-insensitive checks: `Is.EqualTo("abc").IgnoreCase`

### Collections

| Constraint | Verifies |
| ---------- | -------- |
| `Has.Count(n)` | Collection has `n` items |
| `Has.Member(obj)` | Collection contains `obj` |
| `Has.All...` | All items satisfy constraint |
| `Has.Some...` | At least one item satisfies |
| `Has.None...` | No items satisfy |

Examples:

```csharp
Assert.That(list, Has.Count(3));
Assert.That(list, Has.Member("expected"));
Assert.That(list, Has.All.GreaterThan(0));
```

### Exceptions

| Constraint | Verifies |
| ---------- | -------- |
| `Throws.InstanceOf<T>()` | Throws `T` or subclass |
| `Throws.TypeOf<T>()` | Throws exactly `T` |
| `Throws.InnerException.InstanceOf<T>()` | Inner exception is `T` |

### Composable Constraints

Use `&` (and) and `|` (or) to combine:

```csharp
Assert.That(result, Is.Not.Null & Has.Count.EqualTo(3));
Assert.That(value, Is.LessThan(0) | Is.EqualTo(42));
```

## `[TestCase]` vs `[TestCaseSource]`

| Situation | Use |
| --------- | --- |
| Simple literals (int, string, bool, enum) | `[TestCase]` |
| Complex objects or computed data | `[TestCaseSource]` |
| Dynamic or external data sources | `[TestCaseSource]` |
| Named test cases | `TestCaseData(...).SetName("...")` |
| Single data row | Never — convert to `[Test]` |

## Fixture Lifecycle

| Attribute | Runs | Typical Use |
| --------- | ---- | ----------- |
| `[OneTimeSetUp]` | Once per class | Expensive setup (DI container, DB seed) |
| `[SetUp]` | Before each test | Fresh state per test |
| `[TearDown]` | After each test | Cleanup per test |
| `[OneTimeTearDown]` | Once per class | Shared resource cleanup |

NUnit creates a **new class instance for each test**. Put per-test setup in `[SetUp]`, not the constructor.

Use `[TestFixture]` when:
- The class inherits from a base test class.
- The class uses constructor injection.
- The class uses `[TestFixtureSource]`.

## Parallelization

| Attribute | Behavior |
| --------- | -------- |
| `[Parallelizable(ParallelScope.Children)]` | Tests in this class run in parallel |
| `[Parallelizable(ParallelScope.Self)]` | Class can run in parallel with other classes |
| `[NonParallelizable]` | This test runs alone, blocking others |

Default: tests within a class run sequentially; classes in different fixtures run in parallel.

## Test Data Patterns

```csharp
// TestCaseData with named parameters
[TestCaseSource(nameof(Parse_InvalidInput_ThrowsException_Test_Data))]
public void Parse_InvalidInput_ThrowsException_Test(string input, Type expectedException)
{
    Assert.That(() => Subnet.Parse(input), Throws.InstanceOf(expectedException));
}

static IEnumerable<TestCaseData> Parse_InvalidInput_ThrowsException_Test_Data()
{
    yield return new TestCaseData(null, typeof(ArgumentNullException))
        .SetName("Parse_NullInput_ThrowsArgumentNullException_Test");
    yield return new TestCaseData("not-valid", typeof(FormatException))
        .SetName("Parse_InvalidFormat_ThrowsFormatException_Test");
}
```
