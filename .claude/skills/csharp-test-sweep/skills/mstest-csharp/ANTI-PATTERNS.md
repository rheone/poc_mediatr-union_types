# MSTest Anti-Patterns

## Missing [TestClass]

Bad — silently ignored by the runner:

```csharp
public class CalculatorTests
{
    [TestMethod]
    public void Add_ReturnsSum_Test() { }
}
```

Good:

```csharp
[TestClass]
public class CalculatorTests
{
    [TestMethod]
    public void Add_ReturnsSum_Test() { }
}
```

## Using [DataTestMethod] Instead of [TestMethod]

Unnecessary in MSTest v3+. `[TestMethod]` works directly with `[DataRow]`.

Bad:

```csharp
[DataTestMethod]
[DataRow(1, 2)]
public void Add_ReturnsSum_Test(int a, int b) { }
```

Good:

```csharp
[TestMethod]
[DataRow(1, 2)]
public void Add_ReturnsSum_Test(int a, int b) { }
```

## Forgetting [DiscoverInternals]

Internal test methods are skipped unless the assembly opts in.

Bad:

```csharp
// No [DiscoverInternals] — test never runs
[TestClass]
internal class ParserTests
{
    [TestMethod]
    internal void Parse_ValidInput_Succeeds_Test() { }
}
```

Good — add `[assembly: DiscoverInternals]` to any file in the project:

```csharp
[assembly: DiscoverInternals]
```

## Ignore Without Comment

Using `[Ignore]` with no explanation makes it unclear when or why to re-enable.

Bad:

```csharp
[Ignore]
[TestMethod]
public void FlakyTest_AlwaysPassesEventually_Test() { }
```

Good — comment explains why:

```csharp
[Ignore] // https://github.com/org/repo/issues/42 — fix pending
[TestMethod]
public void FlakyTest_AlwaysPassesEventually_Test() { }
```

## Inconclusive Without Tracking Item

`Assert.Inconclusive` halts a pass without linking to the work item that tracks the gap.

Bad:

```csharp
Assert.Inconclusive("Not implemented yet");
```

Good:

```csharp
Assert.Inconclusive("Not implemented yet — tracked by #42");
```

## Swapped Expected/Actual

`AreEqual(expected, actual)` — expected is the first argument, actual is the second. Swapping them produces misleading failure messages.

Bad:

```csharp
Assert.AreEqual(actual: 5, expected: result);
// Failure says: Expected 5, got 3 (reversed!)
```

Good:

```csharp
Assert.AreEqual(expected: 5, actual: result);
```

## Overuse of PrivateObject / PrivateType

Testing private members directly indicates a design problem. Prefer testing through the public API.

Bad:

```csharp
var obj = new PrivateObject(new Calculator());
var result = obj.Invoke("AddInternal", 1, 2);
```

Good:

```csharp
var calc = new Calculator();
var result = calc.Add(1, 2);
```
