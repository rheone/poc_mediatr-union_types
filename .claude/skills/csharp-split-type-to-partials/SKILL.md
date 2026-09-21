---
name: csharp-split-type-to-partials
description: Refactors a C# type (class, record, struct) into partial files split by interface, abstract base class, and functional groupings (Factory, Operators, Events, Delegates, nested types). Co-locates private/internal helpers with the grouping that uses them. Mirrors the split in the corresponding test file. Updates the .csproj with DependentUpon nesting for all partials. Use when asked to "split into partials", "refactor by interface", "break up type", or "add partial files" for a C#/.NET type.
license: Apache-2.0
user-invocable: true
metadata:
  author: Robert Engelhardt <rheone@gmail.com>
  version: 2.0.1
---

# csharp-split-type-to-partials

C#/.NET only. Uses subagents to parallelize file reads, writes, and `.csproj` updates where possible.

## Quick start

Invoke with no args — the skill infers the root type from IDE context (the open/selected file whose name matches `{Root}.cs` without a `{Root}.{Something}.cs` pattern). If inference fails, ask the user for the file path.

## Workflow

### 1. Infer & validate

- Identify the root `.cs` file and parse the type declaration
- Confirm the type has **2+ distinct functional groupings** — abort with a clear message if not (see [ORGANIZATION.md](ORGANIZATION.md#threshold))
- Determine scope: **both** type and tests by default; narrow only if user explicitly asks for type-only or tests-only

### 2. Classify members & locate tests (parallel)

Run source classification and test file location **in parallel** using subagents.

**Source member classification** — apply rules from [RULES.md](RULES.md#member-classification) to every member:

- Interface implementation → `{Type}.{BaseInterfaceName}.cs`
- Abstract base class override → `{Type}.{BaseClassName}.cs`
- Static factory method → `{Type}.Factory.cs`
- Operator (including implicit/explicit cast) → `{Type}.Operators.cs`
- Event declaration → `{Type}.Events.cs`
- Public/internal nested delegate → `{Type}.Delegates.cs`
- Public/internal nested type → `{Type}.{NestedTypeName}.cs`
- Private/internal helper used by exactly one grouping → co-located into that grouping's partial
- Everything else (constructors, multi-grouping helpers, constants, private nested types) → root

Group interfaces/base-names that share a base name (ignoring generic arity) into a single partial.
Preprocessor directives travel with their members — see [RULES.md](RULES.md#preprocessor-directives).

**Test file location** — search for `{TypeName}Tests.cs` by walking up to the `.sln` then searching sibling test projects:

- Multiple matches → ask user to disambiguate
- No match → create root test file + partial skeletons

### 3. Classify test methods

Apply three-pass heuristic + TheoryData provider rule from [TESTING.md](TESTING.md#classification).

### 4. Show plan & confirm

Present a full summary before touching any file:

- Files to create (source + test partials)
- Members moving to each partial, including co-location rationale for private/internal helpers
- **Co-location audit:** private/internal members currently in root that are used exclusively by one grouping — list them and ask: "Move these to their co-located partial? (y/n)"
- Test methods moving to each test partial (and which stay with TODO)
- `.csproj` entries to add
- Any nested types flagged as candidates for promotion to top-level types

**Do not proceed until the user confirms.**

### 5. Execute (parallelized)

- **Wave 1 (parallel):** Write all source partial files + write all test partial files
- **Wave 2 (parallel, after Wave 1):** Update source `.csproj` + update test `.csproj`

See [RULES.md](RULES.md#partial-file-template) for source file templates.
See [TESTING.md](TESTING.md#test-partial-template) for test file templates.
See [RULES.md](RULES.md#csproj-nesting) for `.csproj` nesting format.

## Update mode (existing partials)

When partials already exist:

- Create missing partials for newly added groupings; move the new members
- Add/repair missing `DependentUpon` entries in `.csproj` without touching source files
- Run co-location audit and include results in the plan (flag, never auto-fix)
- **Never** re-sort members between existing partials unless explicitly asked to "reclassify all members" — in that case, treat as normal execution but with an extra confirmation step highlighting all members that will move between existing partials
