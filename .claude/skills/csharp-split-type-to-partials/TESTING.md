# csharp-split-type-to-partials — Testing Guide

## Test partial template {#test-partial-template}

```csharp
using Xunit;

namespace My.Namespace.Tests
{
    /// <content>
    ///     <see cref="MyType"/> tests for <see cref="IMyInterface{MyType}"/> and <see cref="IMyInterface"/>
    /// </content>
    public partial class MyTypeTests
    {
        // moved or skeleton test methods
    }
}
```

- Test partials are `partial class {TypeName}Tests` — no interface list on the declaration
- `/// <content>` describes which grouping is under test

### `/// <content>` text by grouping

| Grouping | `/// <content>` text |
|---|---|
| Interface | `<see cref="{Type}"/> tests for <see cref="{Interface}"/>` — list all grouped interfaces |
| Abstract base class | `<see cref="{Type}"/> tests for <see cref="{BaseClass}"/>` |
| Factory | `<see cref="{Type}"/> tests for static factory methods` |
| Operators | `<see cref="{Type}"/> tests for operators` |
| Events | `<see cref="{Type}"/> tests for events` |
| Delegates | `<see cref="{Type}"/> tests for delegate type declarations` |
| Nested type | `<see cref="{Type}"/> tests for nested type <see cref="{NestedType}"/>` |

## Test method classification {#classification}

Run three passes in order — first match wins.

### Pass 0 — name-prefix scan (Events, Delegates, nested types)

Before anything else, check for the new groupings:

- Method name starts with `{EventName}` or contains `_Event_` → `{Type}.EventsTests.cs`
- Method name starts with or contains `{DelegateName}` → `{Type}.DelegatesTests.cs`
- Method name starts with `{NestedTypeName}` → `{Type}.{NestedTypeName}Tests.cs`

If the corresponding test partial already exists on disk, match is automatic for that grouping's names.

### Pass 1 — method name

If the test method name contains the name of a member of a grouping (e.g. `CompareTo`, `Equals`, `GetHashCode`, `ToString`, `Parse`, `TryParse`, or any interface/abstract method name), assign it to the corresponding partial.

### Pass 2 — body scan

If Pass 1 is inconclusive, scan the test body for direct calls to grouping members. Assign to the first unambiguous match.

### Fallback

If all passes are ambiguous or produce multiple matches, leave the test in the root file with:

```csharp
// TODO: manually move to appropriate test partial
```

## TheoryData provider rule

`TheoryData`-provider methods are satellites of their `[Theory]`. Identify providers by:

- Name pattern `{TestMethodName}_TestCases` or `{TestMethodName}TestCases`
- `[MemberData(nameof(X))]` attribute on the `[Theory]` pointing to the provider

**Rule:** Classify the `[Theory]` first using the three-pass heuristic. The provider travels with it unconditionally — never classify a provider independently. A provider whose `[Theory]` cannot be classified stays in root alongside its `[Theory]`.

## Regions within test partials

Test partials especially benefit from `#region` wrappers since they aggregate many test methods. Add regions when there are semantic sub-groupings:

- **Operators partial:** separate region per operator category

  ```csharp
  #region Equality Operators
  #region Comparison Operators
  #region Increment Operators
  ```

- **Factory partial:** separate region per method under test

  ```csharp
  #region Parse
  #region TryParse
  ```

- **Interface partials with many members:** region per member under test

  ```csharp
  #region CompareTo
  #region GetHashCode
  ```

## csproj nesting — test project {#csproj-nesting}

```xml
<ItemGroup>
  <!-- Nest {Type}.*Tests.cs under {TypeName}Tests.cs -->
  <Compile Update="{Type}.IMyInterfaceTests.cs">
    <DependentUpon>{TypeName}Tests.cs</DependentUpon>
  </Compile>
  <Compile Update="{Type}.BaseClassNameTests.cs">
    <DependentUpon>{TypeName}Tests.cs</DependentUpon>
  </Compile>
  <Compile Update="{Type}.FactoryTests.cs">
    <DependentUpon>{TypeName}Tests.cs</DependentUpon>
  </Compile>
  <Compile Update="{Type}.OperatorsTests.cs">
    <DependentUpon>{TypeName}Tests.cs</DependentUpon>
  </Compile>
  <Compile Update="{Type}.EventsTests.cs">
    <DependentUpon>{TypeName}Tests.cs</DependentUpon>
  </Compile>
  <Compile Update="{Type}.DelegatesTests.cs">
    <DependentUpon>{TypeName}Tests.cs</DependentUpon>
  </Compile>
  <Compile Update="{Type}.{NestedType}Tests.cs">
    <DependentUpon>{TypeName}Tests.cs</DependentUpon>
  </Compile>
</ItemGroup>
```

## Update mode — test co-location audit

When running in update mode on existing test partials:

- Flag test methods in the root test file that clearly belong to an existing test partial (by Pass 0/1/2 match)
- Flag orphaned `TheoryData` providers whose `[Theory]` has moved to a partial
- Include both in the plan with a prompt — never auto-move
