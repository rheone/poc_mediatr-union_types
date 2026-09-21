# csharp-split-type-to-partials — Organization Guide

## Why split into partials?

The partial-file pattern makes large and small types equally navigable:

- Each file has a single, named responsibility — you always know where to look
- Co-located private helpers eliminate "where does this live?" — helpers live next to the code that calls them
- IDE file nesting via `DependentUpon` keeps the project tree clean
- All overloads of a method should grouped together
- Test partials mirror source partials exactly — a reader finds tests for any grouping instantly
- All test methods and their datasources should grouped together

## Threshold — when to split {#threshold}

Split when the type has **2 or more distinct functional groupings**. A grouping is any of:

| Grouping | Counts as one grouping when... |
|---|---|
| Implemented interface | The type implements it (regardless of member count) |
| Abstract base class | The type overrides 1+ abstract members from it |
| Factory | 1+ static methods returning the declaring type (or `bool`+`out` of the declaring type) |
| Operators | 1+ `operator` declarations |
| Events | 1+ `event` declarations |
| Delegates | 1+ public/internal nested `delegate` declarations |
| Nested type | Each public/internal nested type is its own grouping |

A type with only one grouping does not meet the threshold — abort and explain why.

## Co-location philosophy

Private and internal behavioral helpers should live as close as possible to the code that uses them. Co-locating into a partial:

- Makes the partial self-contained and readable without cross-file navigation
- Signals intent: "this helper exists exclusively to serve this grouping"
- Keeps root lean — only constructors, broadly-used helpers, and shared state remain there

**Tie-breaker:** When a helper is used by 2+ groupings, root is always the right home. Never duplicate a helper; never pick "most used" over root.

## When NOT to split

- The type has fewer than 2 groupings
- The type is generated code (designer-generated, source-generated) — do not split generated files
- A newly added grouping has only 1–2 trivial members — prefer adding them to the closest existing partial rather than creating a new one (use judgement; confirm with user if ambiguous)

## Nested type guidance

Every public/internal nested type gets its own file (`{Type}.{NestedType}.cs`). Private nested types stay in root.

File naming for deeper nesting: `{Type}.{NestedType}.{DeepNestedType}.cs` — each level adds a segment.

**No recursive splitting.** If a nested type itself has 2+ groupings and would benefit from the pattern, flag it in the plan:

> "`{NestedType}` meets the splitting criteria. Consider promoting it to a top-level type, then invoke the skill separately."

Do not recursively split nested types.

## Abstract base classes vs. interfaces

Treat abstract base class overrides identically to interface implementations — each abstract base class gets its own partial (`{Type}.{BaseClassName}.cs`) with the same `/// <content>` and template. Rationale: an abstract base class is a contract boundary, not an implementation detail, and deserves the same visibility.

## File naming conventions

| Grouping | File name |
|---|---|
| Interface (or interface family) | `{Type}.{BaseInterfaceName}.cs` |
| Abstract base class | `{Type}.{BaseClassName}.cs` |
| Factory | `{Type}.Factory.cs` |
| Operators | `{Type}.Operators.cs` |
| Events | `{Type}.Events.cs` |
| Delegates | `{Type}.Delegates.cs` |
| Nested type | `{Type}.{NestedTypeName}.cs` |
| Nested type (deeper) | `{Type}.{NestedTypeName}.{DeepNestedTypeName}.cs` |
