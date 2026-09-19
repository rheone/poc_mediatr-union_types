# MSTest API Reference

## Assert Methods

| Method | Purpose |
|--------|---------|
| `AreEqual(expected, actual)` | Value equality |
| `AreEqual(expected, actual, delta)` | Floating-point tolerance |
| `AreNotEqual(expected, actual)` | Value inequality |
| `AreSame(expected, actual)` | Reference equality |
| `AreNotSame(expected, actual)` | Reference inequality |
| `IsTrue(condition)` | Boolean true |
| `IsFalse(condition)` | Boolean false |
| `IsNull(obj)` | Null check |
| `IsNotNull(obj)` | Non-null check |
| `IsInstanceOfType(obj, typeof(T))` | Type check |
| `IsNotInstanceOfType(obj, typeof(T))` | Negative type check |
| `ThrowsException<T>(Action)` | Exact exception type |
| `ThrowsExceptionAsync<T>(Func<Task>)` | Async exception |
| `Fail(message)` | Force failure |
| `Inconclusive(message)` | Mark as inconclusive |
| `Multiple(Action)` | Batch independent assertions |

## StringAssert Methods

| Method | Purpose |
|--------|---------|
| `Contains(actual, substring)` | Substring present |
| `DoesNotContain(actual, substring)` | Substring absent |
| `Matches(actual, Regex)` | Regex match |
| `DoesNotMatch(actual, Regex)` | Regex no match |
| `StartsWith(actual, prefix)` | Prefix match |
| `EndsWith(actual, suffix)` | Suffix match |

## CollectionAssert Methods

| Method | Purpose |
|--------|---------|
| `AreEqual(expected, actual)` | Same elements, same order |
| `AreNotEqual(expected, actual)` | Different elements/order |
| `AreEquivalent(expected, actual)` | Same elements, any order |
| `AreNotEquivalent(expected, actual)` | Different element set |
| `AllItemsAreInstancesOfType(coll, typeof(T))` | All elements are type T |
| `AllItemsAreNotNull(coll)` | No null elements |
| `AllItemsAreUnique(coll)` | No duplicates |
| `Contains(coll, element)` | Element present |
| `DoesNotContain(coll, element)` | Element absent |
| `IsSubsetOf(subset, superset)` | All subset elements in superset |
| `IsNotSubsetOf(subset, superset)` | Some subset element not in superset |

## Lifecycle Attributes

| Attribute | Scope | Signature | Runs |
|-----------|-------|-----------|------|
| `[TestInitialize]` | Per-test | `public void Method()` | Before each `[TestMethod]` |
| `[TestCleanup]` | Per-test | `public void Method()` | After each `[TestMethod]` |
| `[ClassInitialize]` | Class | `public static void Method(TestContext)` | Once before first test in class |
| `[ClassCleanup]` | Class | `public static void Method()` | Once after last test in class |
| `[AssemblyInitialize]` | Assembly | `public static void Method(TestContext)` | Once before any test in assembly |
| `[AssemblyCleanup]` | Assembly | `public static void Method()` | Once after all tests in assembly |

## Execution Control

| Attribute | Purpose |
|-----------|---------|
| `[Timeout(int ms)]` | Fail if test exceeds duration |
| `[Retry(int count)]` | Retry on failure |
| `[STATestMethod]` | Run on STA thread |
| `[STATestClass]` | All methods run on STA thread |
| `[Priority(int)]` | Test ordering (VS Test Explorer) |
| `[DoNotParallelize]` | Exclude from parallel execution |

## Metadata Attributes

| Attribute | Purpose |
|-----------|---------|
| `[TestCategory("name")]` | Category grouping |
| `[TestProperty("k", "v")]` | Custom key-value pair |
| `[Owner("name")]` | Responsible person |
| `[WorkItem(id)]` | Azure DevOps work item link |
| `[GitHubWorkItem(id)]` | GitHub issue link |
| `[Description("text")]` | Descriptive text |
