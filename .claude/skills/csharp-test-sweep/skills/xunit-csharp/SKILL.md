# xUnit v3

xUnit-specific rules, applied after the [General Quality Checklist](../../references/quality-checklist.md). Lookup files: [REFERENCE.md](REFERENCE.md) (serializers, assertion rewrites), [EXAMPLES.md](EXAMPLES.md), [ANTI-PATTERNS.md](ANTI-PATTERNS.md), `references/`.

Tests are unit tests unless the sweep detected an integration project.

## Checklist

- [ ] `[Fact]` for one scenario; `[Theory]` for two or more distinct partitions or boundaries
- [ ] `TheoryData<T1, ...>` with `[MemberData]`, field named `{TestMethodName}_Test_Data`; `[InlineData]` only for primitive literals ([theorydata.md](references/theorydata.md) converts `object[]` sources)
- [ ] One row per equivalence partition plus boundaries; extra rows only where branching inside a partition demands them. The `<summary>` names each row's partition and the reason for any extra.
- [ ] `Assert.Equivalent` for structural comparison of types without `Equals`; `Assert.Equal` when the type has value equality. Production types keep their own `Equals`.
- [ ] `Assert.Multiple` for independent properties of one scenario; separate scenarios stay separate tests
- [ ] `Assert.Collection` / `Assert.All` in place of `foreach` + `Assert`
- [ ] Exception assertions return the exception and check a property:

  ```csharp
  var ex = Assert.Throws<ArgumentNullException>(() => Subnet.Parse(null));
  Assert.Contains("input", ex.ParamName);
  ```

- [ ] v3 API only (see upgrade table)
- [ ] `xunit.analyzers` referenced
- [ ] Integration tests carry `[Trait("Category", "Integration")]`

## Rules

**Assert.Multiple**

```csharp
Assert.Multiple(
    () => Assert.Equal(expectedHead, result.Head),
    () => Assert.Equal(expectedTail, result.Tail)
);
```

**Cancellation**: cancel a `CancellationTokenSource` and assert `OperationCanceledException` ([EXAMPLES.md](EXAMPLES.md), Example 4).

**Test output**: `ITestOutputHelper` arrives through the primary constructor (`public class Tests(ITestOutputHelper output)`) only in classes that log diagnostic context.

**Fixtures**: never `.Result` / `.GetAwaiter().GetResult()` in setup; use `IAsyncLifetime`. Wiring in [Fixtures.md](references/Fixtures.md).

| Situation | Use |
|---|---|
| Cheap, pure setup | Constructor / `IDisposable` |
| Async setup or teardown | `IAsyncLifetime` |
| Expensive setup shared per class | `IClassFixture<T>` |
| Shared across classes | `ICollectionFixture<T>` + `[Collection]` |

**Object mother**: named canonical instances; each call returns a new instance. Colocate for single-class use, `TestData/` for project-wide use ([ObjectMother.md](references/ObjectMother.md)).

**Private and internal members**: test through the public API; `[assembly: InternalsVisibleTo]` for `internal`; reflection last.

**Integration tests** (when detected): `IClassFixture<T>` / `ICollectionFixture<T>` for shared resources, each test leaves infrastructure clean (`IAsyncLifetime.DisposeAsync`), real infrastructure rather than mocks, sharing classes grouped under `[Collection]`.

## v1/v2 → v3

| v1/v2 | v3 |
|---|---|
| `IXunitSerializable` on the type | `IXunitSerializer` + `[RegisterXunitSerializer]` |
| `[assembly: CollectionBehavior]` | Removed; parallelism is on by default |
| `xunit.runner.visualstudio` | v3-compatible runner package |
