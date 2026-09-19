# xUnit v3 Anti-Patterns

xUnit-specific pitfalls. Framework-agnostic ones live in the [General Quality Checklist](../../references/quality-checklist.md).

---

## 1. `Console.WriteLine` in Tests

xUnit v3 suppresses `Console.WriteLine`; the output goes nowhere. Use `ITestOutputHelper` for diagnostics.

```csharp
// BAD — silent output
Console.WriteLine($"Testing with input: {input}");

// GOOD — visible diagnostic output
_output.WriteLine($"Testing with input: {input}");
```

---

## 2. `[assembly: CollectionBehavior(DisableTestParallelization = true)]`

Disabling parallelism globally hides flaky-test symptoms and leaves the root cause. Isolate the state, or put only the conflicting classes in a named `[Collection]`.

```csharp
// BAD
[assembly: CollectionBehavior(DisableTestParallelization = true)]

// GOOD — isolate only the conflicting classes
[Collection("DatabaseTests")]
public sealed class SlowDatabaseTests { ... }
```

---

## 3. `ITestOutputHelper` in Shared Fixtures

A fixture that outlives a single test and captures `ITestOutputHelper` accumulates output across tests. Inject it only into per-test constructors.

```csharp
// BAD — captured for the class lifetime
public sealed class MyFixture(ITestOutputHelper output)
{
    public ITestOutputHelper Output { get; } = output;
}
```

---

## 4. `ITestOutputHelper` in Static Theory Data

Data factories are static and cannot receive constructor injection. Move diagnostic output into the test body.

```csharp
// BAD — does not compile
public static TheoryData<string> Data
{
    get
    {
        _output.WriteLine("generating data...");
        return new TheoryData<string> { "a", "b" };
    }
}
```
