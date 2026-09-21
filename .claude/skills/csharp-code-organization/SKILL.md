---
name: csharp-code-organization
description: Reorganizes C# member ordering and file/type structure to a consistent convention - constants-fields-ctors-properties-events-methods grouping, partial-class splitting, generic-arity file naming, region grouping for interface implementations, single-use helper colocation. Detects the target repo's StyleCop/.editorconfig ordering rules first and never contradicts an explicit one. Supports type, file, namespace, whole-repo, or since-a-git-marker sweep scope, or applies inline while writing/editing a type. Portable across C# repos - re-reads config per repo, no hardcoded assumptions about which StyleCop rules are on.
disable-model-invocation: true
license: Apache-2.0
user-invocable: true
metadata:
  author: Robert Engelhardt <rheone@gmail.com>
  version: 1.0.0
---

# C# Code Organization

## Scope

Scope is inferred from invocation arguments. Default is **inline** - apply the rules to whatever type or file is already being written or edited for another task, nothing more.

| Scope         | Invocation                                          | Behavior                                          |
| ------------- | ---------------------------------------------------- | -------------------------------------------------- |
| Inline        | _(implicit, while doing other work on a C# file)_    | Apply rules to the file(s) already being touched  |
| Type          | `/csharp-code-organization TypeName`                 | One type and all of its partial-class files       |
| File          | `/csharp-code-organization path/to/File.cs`          | One file                                          |
| Namespace     | `/csharp-code-organization My.Namespace`             | All types in that namespace/directory             |
| Full sweep    | `/csharp-code-organization`                          | Every `.cs` file in the repo (excluding generated) |
| Since marker  | `/csharp-code-organization --since <git-ref>`        | Only `.cs` files changed since `<git-ref>`        |

This skill never fires on its own - it only runs when named explicitly, by slash command or by a request that clearly means "organize/reorganize/restructure this code." Editing a method body, fixing a bug, or any other C# edit that isn't about layout is not a trigger.

## Discovery Phase

Run once before applying any rule, regardless of scope.

1. **Read the target repo's ordering config** - `.editorconfig` and `stylecop.json` (or the repo's equivalent linter config, if not StyleCop). For each of SA1201, SA1202, SA1203, SA1204, SA1123, SA1124, SA1214, SA1216 (or that linter's equivalents), record one of:
   - **Explicit** - a severity other than the tool's default is set (enabled *or* suppressed). This repo has taken a position; never act against it.
   - **Unset** - left at default, no override found. The repo has taken no position; this skill's advisory rules (below) fill the gap.

   Never assume another repo matches one you've swept before - re-read every time. See [references/member-ordering.md](references/member-ordering.md) for what each rule ID governs and exactly what "acting against it" means.

2. **Check for existing region usage** - if `#region` appears nowhere in the codebase yet, treat region-based grouping as a pattern with no precedent: apply it only to files this invocation actually touches, never as a reason to open untouched sibling files.

3. **Check for an existing partial-class naming scheme** - if the repo already splits types into partials with its own naming convention, follow that convention instead of the canonical one in [references/file-and-type-structure.md](references/file-and-type-structure.md). Absent one, use the canonical scheme.

4. **Since-marker scope only** - resolve the file list with `git diff --name-only <git-ref>...HEAD -- '*.cs'`.

## Rules

Apply in this order:

1. **Config wins.** Never restructure in a way that contradicts a rule Discovery marked **Explicit**. An explicit *suppression* is not silence to fill - it's a position the repo has already taken (see [references/member-ordering.md](references/member-ordering.md) for the "exception proves the rule" case: a suppressed rule elsewhere may still be enabled here).
2. **Member ordering** within a type/file - see [references/member-ordering.md](references/member-ordering.md).
3. **File and type structure** - one-type-per-file, arity naming, partials, regions, test naming - see [references/file-and-type-structure.md](references/file-and-type-structure.md).
4. **Never touch generated or scaffolded code** - source-generator output, EF Core compiled models, designer files. Skip silently; don't ask.

## Sweep Loop

Applies to Type/File/Namespace/Full/Since-marker scopes. Skip for Inline scope - just apply the rules directly.

Process units in **directory order, then alphabetical within each directory**.

For each unit:

1. Apply member-ordering rules.
2. Apply file/type-structure rules - split into partials, arity files, or regions only when that reference's trigger criteria are actually met; don't split preemptively.
3. **Verify** - `dotnet build` the affected project(s), then run the affected tests. This is mandatory before moving to the next unit - reordering members or splitting a type into partials can silently change behavior (static field init order across partial files, a doc comment lost in the move). If the build breaks and the fix isn't immediately obvious, restore the file (`git checkout -- {file}`) and make a smaller, targeted change instead of forcing the full reorganization through.

## End of Sweep

1. Write a prose summary per unit: what was reorganized, and why (which rule applied, which exception was invoked if any).
2. Run a final full `dotnet build` + `dotnet test` pass across the whole solution as a last verification.

---

**Reference files:**

| Reference                                                                     | Contents                                                                                  |
| ------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------ |
| [references/member-ordering.md](references/member-ordering.md)               | Canonical member order, accessibility/static grouping, colocation and overload exceptions |
| [references/file-and-type-structure.md](references/file-and-type-structure.md) | One-type-per-file, arity naming, partial-class convention, regions, test naming, out-of-scope list |
