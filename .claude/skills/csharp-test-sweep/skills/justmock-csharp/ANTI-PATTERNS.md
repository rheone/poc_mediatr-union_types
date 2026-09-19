# JustMock Anti-Patterns

## 1. Mocking the Class Under Test (Free Mode)

`Mock.Create<T>()` intercepts virtual members on `T`. When `T` is the class under test, the method under test never executes — the test always passes vacuously.

```csharp
// BROKEN: AbstractProcessor.Process is virtual, never executes
var sut = Mock.Create<AbstractProcessor>();
var result = sut.Process("input");  // returns default — real code never runs
Assert.Equal("expected", result);    // fails vacuously
```

**Fix**: use a concrete subclass that inherits the implementation.

**Exception**: `Mock.Create<AbstractClass>()` is correct when the abstract class is a **dependency** being injected into the subject under test, not the subject itself.

## 2. Mocking the Class Under Test (Elevated Mode)

In Elevated mode, **all** methods (non-virtual, sealed, private) are intercepted — the trap is even harder to spot because the mock succeeds silently.

```csharp
// SUBTLE BUG: ConcreteProcessor has no virtual members, but Elevated mode
// intercepts everything. Process() never runs.
var sut = Mock.Create<ConcreteProcessor>();
var result = sut.Process("input"); // intercepted — never executes
```

**Fix**: never use `Mock.Create<T>()` when `T` is the class under test, regardless of mode. Instantiate the real class instead.

## 3. Using Elevated-Only Features in Free Mode

Code compiles, tests build, but throw `MockException` at runtime because the profiler is absent. This is the most common JustMock-specific support issue.

```csharp
// Compiles in Free mode — throws at runtime
Mock.Arrange(() => DateTime.Now).Returns(fixedDate);
Mock.Create<SealedClass>();
```

**Fix**: check the project's JustMock mode (Free vs Elevated) before using these features. Run a smoke test to confirm profiler is wired.

## 4. Over-Using Elevated Mode

Elevated mode can mock anything — statics, sealed classes, `DateTime.Now`, private methods. Using it for everything creates tests that are:
- **Fragile** — internal changes break tests
- **Version-locked** — tied to the commercial license
- **Design-hiding** — dependencies that should be injected are hidden behind static calls

```csharp
// BAD: Elevated mode masks a design problem
Mock.Arrange(() => File.ReadAllText("config.json")).Returns("{}");

// BETTER: inject an IFileSystem abstraction
// (works in Free mode, doesn't need a license, enables clean DI)
```

**Fix**: prefer dependency injection and interface abstractions. Reserve Elevated mode for third-party code you cannot change or legacy systems under migration.

## 5. Forgetting Occurs Verification

Setting up a mock without asserting that the call actually happened is a common gap:

```csharp
// INCOMPLETE: Save is arranged but never verified
Mock.Arrange(() => repo.Save(Arg.IsAny<User>())).DoInstead(...);
service.CreateUser("bob");
// No Mock.Assert — did Save get called?
```

**Fix**: every arranged call that is expected to execute must have a corresponding `Mock.Assert(...)`.

## 6. Loose Mode Silently Returning Null

Loose mode returns type defaults for unarranged calls. When a method returns an object, the default is `null` — this can cause downstream `NullReferenceException` or silently wrong results.

```csharp
// Loose mode: repo.GetById returns null unless arranged
var sut = new UserService(repo);
var result = sut.GetUser(1); // result is null — silent failure or NRE
```

**Fix**: either arrange all calls the test depends on, or switch to `Behavior.Strict` during test development to identify missing arrangements.

## 7. Testing Implementation Details via Elevated Mode

Elevated mode allows mocking private and internal methods. Testing these ties the test to the implementation, not the behavior:

```csharp
// BROKEN: tests implementation detail
Mock.NonPublic.Arrange<int>(sut, "CalculateInternal", 42).Returns(100);

// BETTER: test the public behavior that uses CalculateInternal
var result = sut.PublicMethod(42);
```

**Fix**: test the public contract. If a private method is complex enough to warrant direct testing, extract it into its own testable class.
