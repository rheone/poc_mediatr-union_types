# Moq Anti-Patterns

## 1. Mocking the Class Under Test

See the [General Quality Checklist](../../references/quality-checklist.md) for why.

```csharp
// BROKEN: never executes the real Process logic
var mock = new Mock<OrderProcessor>();
mock.Setup(x => x.Process(order)).CallBase(); // easy to forget
var result = mock.Object.Process(order);

// FIXED: use the real class or a concrete subclass
var sut = new OrderProcessor();
var result = sut.Process(order);
```

Need partial mocking of the subject? Extract the dependency instead.

## 2. CallBase as a Workaround

`.CallBase()` calls the real implementation on virtual methods. When used on the class under test, it indicates the test should be using the real class directly.

```csharp
// Fragile — one missing .CallBase() and the test silently returns default
var mock = new Mock<AbstractProcessor>();
mock.Setup(x => x.Process("a")).CallBase();  // must remember for every setup

// Prefer a concrete subclass
var sut = new ConcreteProcessor();
```

## 3. VerifyAll as Default

`mock.VerifyAll()` verifies every setup marked `.Verifiable()`. This over-specifies tests — changes to the SUT's internal call pattern break tests that should be unaffected.

```csharp
// Fragile: adding a new dependency call breaks this test
mock.Setup(r => r.Find(1)).Returns(user).Verifiable();
mock.Setup(r => r.Log("start")).Verifiable();
service.Run();
mock.VerifyAll();

// Prefer targeted Verify for the calls that matter
mock.Verify(r => r.Find(1), Times.Once());
```

## 4. Overusing Strict Mode

`MockBehavior.Strict` throws on any unsetup call. This makes tests brittle — every internal implementation change (adding a logging call, reading a config property) breaks the test.

```csharp
// Fragile: any unexpected call throws
var mock = new Mock<IDependency>(MockBehavior.Strict);
mock.Setup(x => x.MethodA()).Returns(1);
// SUT calls MethodB — throws MockException even though MethodB was irrelevant

// Prefer Loose, setup only what the test cares about
var mock = new Mock<IDependency>(MockBehavior.Loose);
mock.Setup(x => x.MethodA()).Returns(1);
```

## 5. Returns(null) for Reference Types

`Returns(null)` is redundant for reference types in Loose mode — Moq already returns `null`. It adds noise without changing behavior.

```csharp
// Redundant
mock.Setup(x => x.Find(99)).Returns((User?)null);

// Cleaner — let Loose return null by default, or document intent:
// mock is Loose; Find returns null for nonexistent users by default
```

## 6. Loose Mode Hiding Failures

Loose mode silently returns `null`/`0`/`false` for unsetup calls. Tests can pass when the SUT uses an unsetup dependency and gets a default value that happens to work.

```csharp
var mock = new Mock<IDiscountService>();
mock.Setup(x => x.CalculateDiscount("PRIMARY")).Returns(0.1m);
// Forgot to setup "SECONDARY" — Loose returns 0.0m silently
var result = service.Calculate("SECONDARY"); // always 0 — may hide bugs
```

**Mitigation**: use `It.Is<T>(p)` to ensure all expected inputs are covered, or switch to Strict for critical paths.

## 7. Callback Without Asserting Captured Values

Capturing with `.Callback()` but never asserting the captured values wastes the setup — the test doesn't verify anything about the captured data.

```csharp
var captured = "";
mock.Setup(x => x.Log(It.IsAny<string>()))
    .Callback<string>(s => captured = s);
service.Run();
// forgot to assert on captured — test passes regardless
```

Always assert on captured values, or use `Verify` instead of `Callback`.

## 8. Unnecessary Setup (Noise)

Setups that are never reached by the test add noise and make the test harder to reason about.

```csharp
// The test only exercises Find, not Save — Save setup is noise
mock.Setup(r => r.Find(1)).Returns(user);
mock.Setup(r => r.Save(It.IsAny<User>()));  // unnecessary
var result = service.Lookup(1);
```

Remove setups that the current test does not exercise.

## 9. Mocking Concrete Class Without Constructor Args

`new Mock<Foo>()` without constructor args passes `default(T)` for each parameter. If `Foo` requires non-null dependencies, the mock silently creates a broken instance.

```csharp
// Foo(string name, ILogger logger)
var mock = new Mock<Foo>();                // passes null, null
var mock2 = new Mock<Foo>("test", logger); // correct
```

Always pass explicit constructor arguments when mocking concrete classes.
