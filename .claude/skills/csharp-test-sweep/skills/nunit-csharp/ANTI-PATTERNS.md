# NUnit v5 Anti-Patterns

## 1. Classic Assert.AreEqual Instead of Constraint-Based

```csharp
// BAD — classic style, less descriptive failure messages
Assert.AreEqual(expected, actual);

// GOOD — constraint-based, richer failure output
Assert.That(actual, Is.EqualTo(expected));
```

NUnit supports both, but constraint-based assertions produce better failure messages and compose with `&`/`|` and `.IgnoreCase`.

## 2. Assert.That with Redundant Boolean Comparison

```csharp
// BAD — double-wrapping the comparison
Assert.That(Is.True(x == y));

// GOOD — let the constraint do the work
Assert.That(x, Is.EqualTo(y));
```

Wrapping `x == y` in `Is.True` defeats the failure message — you get `Expected: True` instead of `Expected: 42`.

## 3. Using Constructor for Test Setup

```csharp
// BAD — NUnit creates one instance per test, but constructor runs before [SetUp]
public class MyTests
{
    private Database _db;
    public MyTests()
    {
        _db = new Database(); // runs before every [SetUp]
    }
}

// GOOD — use [SetUp] for per-test initialization
public class MyTests
{
    private Database _db;

    [SetUp]
    public void SetUp()
    {
        _db = new Database();
    }
}
```

NUnit creates a **new class instance per test**, but the pattern should use `[SetUp]` for clarity and consistency with fixture lifecycle semantics.

## 4. Throwing AssertionException Manually

```csharp
// BAD — brittle, wrong stack trace, no constraint composition
throw new AssertionException("Expected value to be 42");

// GOOD — use the assertion API
Assert.That(value, Is.EqualTo(42));
```

## 5. Multiple Assert.That Outside of Assert.Multiple

```csharp
// BAD — only the first failure is reported; subsequent assertions are skipped
Assert.That(result.Name, Is.EqualTo("expected"));
Assert.That(result.Age, Is.EqualTo(30));
Assert.That(result.Email, Is.EqualTo("a@b.com"));

// GOOD — all failures reported in a single run
Assert.Multiple(() =>
{
    Assert.That(result.Name, Is.EqualTo("expected"));
    Assert.That(result.Age, Is.EqualTo(30));
    Assert.That(result.Email, Is.EqualTo("a@b.com"));
});
```

## 6. Static Mutable State in [OneTimeSetUp]

```csharp
// BAD — static state persists across test classes, causes order-dependent failures
private static int _counter;

[OneTimeSetUp]
public void OneTimeSetUp()
{
    _counter++; // shared state leaked across classes
}

// GOOD — instance fields for per-class state
private int _counter;
private SharedResource _resource;

[OneTimeSetUp]
public void OneTimeSetUp()
{
    _resource = new SharedResource();
}
```

## 7. Assert.Throws Without Verifying the Exception

```csharp
// BAD — any thrown exception passes, even the wrong type
Assert.Throws<Exception>(() => Something());

// GOOD — be specific and verify the exception type
Assert.That(() => Something(), Throws.InstanceOf<ArgumentNullException>());
```
