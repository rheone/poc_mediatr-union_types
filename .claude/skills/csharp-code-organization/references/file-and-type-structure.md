# File and Type Structure

## One type per file

One top-level public type per file, filename matches the type name. Simple record/struct types may keep custom equality/formatting/operator members in the same file as their containing type - this mirrors the exception already stated in the target repo's own conventions (e.g. `AGENTS.md`/`CLAUDE.md`); don't relitigate it, just honor whatever exception the repo already documents.

## Generic-arity exception

Types that share a name but differ by generic arity (`Result<T>` vs `Result<T1, T2>`) are distinct types under the one-type-per-file rule and default to their own file, named `TypeName.<arity>.cs` (e.g. `Result.cs`, `Result.2.cs`, `Result.3.cs`).

**Exception:** when the arities form one tightly-coupled family, authored and evolved together, and each arity is a mechanical extension of the one before it sharing the same shape and file-level intent, keep them in a single file instead of splitting. This mirrors the overloads-together exception in [member-ordering.md](member-ordering.md): the rule exists to stop *unrelated* types from colliding on a filename, not to force apart a family that reads as one design. `OperationOutcome.cs`'s arity-0-through-N union family is the worked example - one file, six arities, because splitting it would scatter one design decision across six files with no independent reason to read any single one alone.

Default to splitting. Only keep a family merged when you can point to the shared shape and shared evolution as the reason - not because splitting is inconvenient in the moment.

## Nested types

Use a nested type only when it is a genuine implementation detail with no meaning outside its containing type (e.g. a private enum that only drives that type's internal state machine). Otherwise, the type is top-level, in its own file, even if today only one caller uses it - meaning "used once" doesn't make a type an implementation detail; only "meaningless outside" does.

## Partial classes

**When to split.** This is a judgment call, not a line-count threshold - a threshold goes stale or misfires (a long but simple union type isn't "large" in the sense that matters; a short type juggling two unrelated concerns is). Split into partials when either is true:

- The type implements two or more interfaces/abstract bases beyond its primary declared purpose, and at least one of those implementations is non-trivial.
- Core state and implementation-specific plumbing are substantial enough that reading either one requires skipping past the other to follow it.

**Naming.** `TypeName.cs` is the primary file - the canonical entry point. It owns:

- The type declaration itself, including all interface/base-type declarations. Interface and base-type names appear **only** on the primary partial declaration, never repeated on an implementation file, unless a generated-code convention requires otherwise.
- Core private/internal constants, static members, and fields.
- Constructors.
- Members that aren't specific to any one interface/base implementation.

Additional files are `TypeName.<Implementation>.cs`, one per interface or base-type implementation (e.g. `Customer.cs` + `Customer.IComparable.cs`). Each implementation file contains only the members that exist to satisfy that one interface/base member, ordered per [member-ordering.md](member-ordering.md) as if it were its own small file.

If the target repo already has its own partial-naming convention in use, follow that instead - see the Discovery Phase in [SKILL.md](SKILL.md).

## Regions

Only if Discovery confirmed the target repo's config permits `#region` (see [member-ordering.md](member-ordering.md)). Use a region for exactly two purposes:

- Grouping the members that implement one interface or override one base type, when they live in the primary file rather than a separate partial file (small implementations that don't yet justify their own `TypeName.<Implementation>.cs`).
- Grouping private/static helpers that belong together but would otherwise clutter the canonical member order (e.g. a block of private static factory methods).

Name every region for exactly what it contains - the interface it implements (`#region IComparable<Customer>`) or the concern it groups (`#region Private Helpers`). A region with a generic or missing name is worse than no region at all: it hides content behind a label that doesn't tell the reader whether to open it. Within a region, apply the canonical order from [member-ordering.md](member-ordering.md).

Regions are not a substitute for the ordering rules generally - don't wrap an entire type's ordinary members in one region "for organization." They exist only for the two cases above.

## Test naming

Test classes are named `<Type>Tests.cs`, mirroring the production type they test, in the corresponding test project at the matching namespace/folder path.

## Out of scope

This skill does not cover, and should not be extended to cover:

- Identifier naming or casing conventions.
- `using`-directive placement or ordering (governed by SA1200 and equivalent rules - read, don't restate).
- Namespace-to-folder / directory layout.
- Generated or scaffolded code - source-generator output, EF Core compiled models, designer files. Never reorganize these; skip them silently during a sweep.
