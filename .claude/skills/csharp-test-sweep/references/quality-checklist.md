# General Quality Checklist

Applies to every test class in every framework. Companion files add framework-specific rules only. Every item applies to every test.

## Structure

- [ ] Name is `{MemberUnderTest}_{Scenario}_{Expectations}_Test`
- [ ] Arrange / Act / Assert comments, all three, in order
- [ ] One logical scenario per test
- [ ] Assert observable behavior (return values, state); verify mock interactions only at dependency boundaries
- [ ] Every test asserts (`Assert.*`, `Received()`, `Verify()`, `Assert.Throws`); a test that arranges a mock, stub, or expectation asserts that expectation
- [ ] Repeated data lives in an object mother; repeated magic values live in named constants (class-level when shared, method-level when single-use)

## Data

- [ ] Cases cover one happy path, one failure/null/invalid path, and the boundaries; one representative per equivalence partition
- [ ] A parameterized test has two or more rows; one row becomes a plain test
- [ ] Data sources are strongly typed; `object[]` rows are converted
- [ ] String parameters cover `null`, `""`, `" "`, `"\t"`/`"\n"`/`"\r"`, and a valid value
- [ ] Every reference-type parameter of a public constructor or method has a `null` test asserting `ArgumentNullException`
- [ ] Parse-roundtrip rows derive their inputs from the same normalized form the expected value uses, never from a raw pre-normalization source
- [ ] Loop-generated rows reduce the source set to one canonical value per equivalence class (**source reduction**); fall back to a `HashSet<string>` keyed on the serialized form only when the full cross-product is needed for coverage

## Determinism and async

- [ ] Async tests return `Task` and `await` (no `async void`, `.Wait()`, `.Result`, `.GetAwaiter().GetResult()`)
- [ ] `CancellationToken.None` (never `default`) when cancellation is not the subject
- [ ] Deterministic conditions in place of `Thread.Sleep` or `Task.Delay(>100ms)`
- [ ] Time, randomness, and identity are injected or seeded (`TimeProvider`, seeded `Random`, fixed `Guid`), never `DateTime.Now` / `Guid.NewGuid()`
- [ ] Each test owns its state; no shared static mutable state

## Assertions

- [ ] Exception tests use the framework's assertion helper (no try/catch) and assert on the message or a property (`Assert.Contains`) so an unrelated exception fails the test
- [ ] Assertion failure messages present when the default output is ambiguous (several similar assertions, or the value alone hides the cause)
- [ ] Test output helpers carry diagnostic context only
- [ ] Skipped or disabled tests carry a written justification and a ticket reference

## Mocks

- [ ] The class under test is always real. A mocking framework intercepts virtual members and returns type defaults, so the method under test never runs and the test passes vacuously. Substitute dependencies only; when the method lives on an abstract base class, instantiate a concrete subclass. Each mocking companion shows its API's fix.
- [ ] Mock call assertions sit in the Assert section only
