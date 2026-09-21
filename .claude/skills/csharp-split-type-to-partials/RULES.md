# csharp-split-type-to-partials — Rules

## Member classification {#member-classification}

| Category | Rule | Destination |
|---|---|---|
| Interface implementation | Method/property whose signature matches an interface member in the type's interface list | `{Type}.{BaseInterfaceName}.cs` |
| Abstract base class override | Method/property marked `override` that satisfies an abstract member of the declared base class | `{Type}.{BaseClassName}.cs` |
| Factory method | `static` method (not an interface/base impl) whose return type is the declaring type **or** whose return type is `bool` with an `out` parameter of the declaring type | `{Type}.Factory.cs` |
| Operator | Declared with `operator` keyword (including `implicit`/`explicit` cast operators) | `{Type}.Operators.cs` |
| Event | `event` declaration (and its backing field + `add`/`remove` accessors) | `{Type}.Events.cs` |
| Public/internal delegate | Nested `delegate` type declaration with public or internal accessibility | `{Type}.Delegates.cs` |
| Public/internal nested type | Nested `class`, `record`, `struct`, or `enum` with public or internal accessibility | `{Type}.{NestedTypeName}.cs` |
| Private/internal helper (single-grouping) | `private` or `internal` method, field, or property used exclusively by members in exactly one grouping's partial | Co-located into that grouping's partial |
| Everything else | Constructors, multi-grouping helpers, constants used by 2+ groupings, private nested types | Root — stays |

### Interface & base class grouping rule
Interfaces that share a base name (ignoring generic arity and containing namespace) are grouped into one partial named after the shared base name:
- `IComparable<T>` + `IComparable` → `{Type}.IComparable.cs`
- `IEquatable<T>` + `IEquatable` → `{Type}.IEquatable.cs`

The partial's class declaration lists **all** grouped interfaces:
```csharp
public sealed partial class Duid : IComparable<Duid>, IComparable
```

Abstract base class implementations always get their own partial, separate from any interfaces.

### Co-location rule {#co-location}
A `private` or `internal` member is co-located into a grouping's partial when it is referenced (directly called or accessed) by members in **exactly one** grouping. Transitive usage does not count — only direct call/access.

If referenced by members in two or more groupings, or by root-destined members → stays in root. Root is always the tie-breaker.

- `internal` *nested types* always get their own file regardless of co-location (structural, not behavioral)
- Constants follow the co-location rule: constant used exclusively by one grouping → travels with it; used by multiple groupings → root

### Preprocessor directives {#preprocessor-directives}
`#if`/`#endif` blocks travel with their members intact — never strip or restructure guards.
- Multiple members from the same `#if` block moving to the same partial → wrap in a single shared `#if` block
- Members from the same `#if` block splitting across partials → each destination gets its own copy of the guard
- Flag this in the plan when it occurs

## Regions within partials

The skill is region-agnostic by default: it neither adds nor removes regions unless clear semantic sub-groupings exist within a partial.

When sub-groupings are present, **add** `#region` / `#endregion` wrappers. Examples:
- `Operators.cs`: separate regions per operator category (arithmetic, comparison, increment/decrement, cast)
- `Events.cs`: separate regions per event or event category
- `Factory.cs`: separate regions for factory methods vs. utility/helper methods

If a private helper moves into a partial that already has a named helper region (e.g. `#region utility methods`), place it inside that existing region.

## Partial file template (source) {#partial-file-template}

```csharp
using System;

namespace My.Namespace
{
    /// <content>
    ///     <see cref="MyType"/> implementation of <see cref="IMyInterface{MyType}"/> and <see cref="IMyInterface"/>
    /// </content>
    public sealed partial class MyType : IMyInterface<MyType>, IMyInterface
    {
        // moved members
    }
}
```

### `/// <content>` text by grouping

| Grouping | `/// <content>` text |
|---|---|
| Interface | `<see cref="{Type}"/> implementation of <see cref="{Interface}"/>` — list all grouped interfaces |
| Abstract base class | `<see cref="{Type}"/> implementation of <see cref="{BaseClass}"/>` |
| Factory | `<see cref="{Type}"/> static factory methods` |
| Operators | `<see cref="{Type}"/> operators` |
| Events | `<see cref="{Type}"/> events` |
| Delegates | `<see cref="{Type}"/> delegate type declarations` |
| Nested type | `<see cref="{NestedType}"/> nested type of <see cref="{Type}"/>` |

Rules:
- Use `/// <content>` (not `/// <summary>`) on every non-root partial
- Include only the `using` directives needed by the members in this file
- Keep the same `namespace` as the root

## csproj nesting — source project {#csproj-nesting}

```xml
<ItemGroup>
  <!-- Nest {Type}.*.cs under {Type}.cs -->
  <Compile Update="{Type}.IMyInterface.cs">
    <DependentUpon>{Type}.cs</DependentUpon>
  </Compile>
  <Compile Update="{Type}.BaseClassName.cs">
    <DependentUpon>{Type}.cs</DependentUpon>
  </Compile>
  <Compile Update="{Type}.Factory.cs">
    <DependentUpon>{Type}.cs</DependentUpon>
  </Compile>
  <Compile Update="{Type}.Operators.cs">
    <DependentUpon>{Type}.cs</DependentUpon>
  </Compile>
  <Compile Update="{Type}.Events.cs">
    <DependentUpon>{Type}.cs</DependentUpon>
  </Compile>
  <Compile Update="{Type}.Delegates.cs">
    <DependentUpon>{Type}.cs</DependentUpon>
  </Compile>
  <Compile Update="{Type}.{NestedType}.cs">
    <DependentUpon>{Type}.cs</DependentUpon>
  </Compile>
</ItemGroup>
```

See [TESTING.md](TESTING.md#csproj-nesting) for test-project `.csproj` nesting.

## Validation

Abort with a clear error message when:
- The type has fewer than 2 distinct functional groupings — splitting is not meaningful (see [ORGANIZATION.md](ORGANIZATION.md#threshold))
- The root file cannot be determined from context and the user does not provide it
- The root file is itself already a `{Root}.{Something}.cs` partial — ask the user for the actual root

## Update mode rules

| Mode | Condition | Action |
|---|---|---|
| Create missing | Partial for grouping X does not exist on disk | Create the partial, move matching members |
| Repair csproj | Partial exists on disk but `DependentUpon` entry is absent | Add the entry; do not modify source files |
| Co-location audit | Private/internal member in root exclusively used by one grouping | Flag in plan, prompt user — never auto-move |
| (skipped) | Member is in the wrong existing partial | Do nothing — never re-sort between existing partials |
