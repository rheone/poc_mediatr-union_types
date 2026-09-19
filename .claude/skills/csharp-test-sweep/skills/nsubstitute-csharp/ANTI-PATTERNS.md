# NSubstitute Anti-Patterns

## 1. Substituting the Class Under Test

The [General Quality Checklist](../../references/quality-checklist.md) explains why; the NSubstitute fix:

```csharp
// BROKEN: Substitute.For<AbstractIPAddressRange> intercepts virtual ToString
var range = Substitute.For<AbstractIPAddressRange>(head, tail);
var result = range.ToString("G", CultureInfo.CurrentCulture); // always ""
Assert.Equal("192.168.1.1 - 192.168.1.42", result);          // always FAILS

// FIXED: concrete subclass inherits the implementation
var range = new IPAddressRange(head, tail);
var result = range.ToString("G", CultureInfo.CurrentCulture);
Assert.Equal("192.168.1.1 - 192.168.1.42", result);
```

## 2. Arg.Any Overuse

`Arg.Any<T>()` matches everything, so it cannot verify specific argument values. Overuse makes tests meaningless — they pass no matter what input is passed.

```csharp
// BROKEN: doesn't verify the actual input
repo.Received(1).SaveAsync(Arg.Any<User>());

// BETTER: verify specific argument
repo.Received(1).SaveAsync(Arg.Is<User>(u => u.Name == "Alice"));

// Arg.Any is acceptable for: CancellationToken (when not testing cancellation),
// complex objects verified by separate assertions, or ordering-only tests.
```

## 3. Setup Without Assertion

Configuring a return value with `Returns` but never calling `Received`/`DidNotReceive` means the interaction is untested.

```csharp
// BROKEN: stubbed but never verified
repo.Search("test").Returns(["a", "b"]);
sut.DoWork();
// Did Search actually get called?

// FIXED: verify the call occurred
repo.Search("test").Returns(["a", "b"]);
sut.DoWork();
repo.Received(1).Search("test");
```

## 4. Over-Using ForPartsOf

`Substitute.ForPartsOf<T>()` calls through to real implementations by default. Frequent use suggests the design should extract an interface rather than partially mock a concrete class.

```csharp
// SMELL: cherry-picking virtual methods to stub
var svc = Substitute.ForPartsOf<ConcreteService>();

// PREFER: extract an interface and substitute that instead
public interface IService { ... }
var svc = Substitute.For<IService>();
```

## 5. Returns(null) for Value Types

`Returns(null)` on a non-nullable value type either won't compile or silently returns the default (0, false).

```csharp
// COMPILE ERROR: cannot convert null to int
sub.GetCount().Returns(null);

// CORRECT: return default or a specific value
sub.GetCount().Returns(0);
sub.GetCount().Returns(default(int));
```

## 6. Shared Substitute State Between Tests

When a substitute is reused across tests (e.g., a field in a shared test class), call history accumulates and tests become order-dependent.

```csharp
[Test]
public void Test1()
{
    repo.Search("a").Returns(["x"]);
    sut.Run();
    repo.Received(1).Search("a"); // passes
}

[Test]
public void Test2()
{
    // repo still has call history from Test1!
    sut.Run();
    repo.Received(1).Search("a"); // fails — received 2 total calls, expected 1
}

// FIX: repo.ClearReceivedCalls() in setup, or recreate per test
```

## 7. Returns on a Non-Virtual Member

NSubstitute only intercepts `virtual`, `abstract`, or interface members. Calling `.Returns` on a non-virtual member has no effect — the real implementation still runs.

```csharp
public class FileReader
{
    public string ReadFile(string path) => File.ReadAllText(path); // non-virtual
}

// BROKEN: Returns has no effect; real ReadFile executes
var reader = Substitute.For<FileReader>();
reader.ReadFile(Arg.Any<string>()).Returns("stubbed");

// FIX: extract an interface and substitute that
var reader = Substitute.For<IFileReader>();
reader.ReadFile(Arg.Any<string>()).Returns("stubbed");
```
