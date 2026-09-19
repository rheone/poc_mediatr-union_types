---
name: csharp-test-sweep
description: Sweep C# test classes one at a time, raising each to the quality checklist. Detects the test framework and mocking library from the project file and applies the matching companion rules. Use to sweep, audit, review, or add coverage to a C# test suite, at test, class, namespace, or project scope.
license: Apache-2.0
user-invocable: true
metadata:
  author: Robert Engelhardt <rheone@gmail.com>
  version: 3.0.0
---

# C# Test Sweep

## Scope

Inferred from the invocation argument; default is **full sweep**. Update mode applies at every scope: improve existing tests, then add missing ones. Scopes narrower than class skip sub-agents, the project prompt, and Discovery.

| Scope        | Invocation                                 |
| ------------ | ------------------------------------------ |
| Single test  | `/csharp-test-sweep ClassName.Method_Test` |
| Region/group | `/csharp-test-sweep ClassName#MemberName`  |
| Class        | `/csharp-test-sweep ClassName`             |
| Namespace    | `/csharp-test-sweep My.Namespace`          |
| Full sweep   | `/csharp-test-sweep`                       |

## Configuration

**Detect** first:

- Test projects: `.csproj` files referencing `xunit.v3`, `xunit`, `NUnit`, or `MSTest.TestFramework`
- Per project: test framework, mocking library, `<TreatWarningsAsErrors>`, target frameworks
- Integration tests: `*.IntegrationTests` project name, or `WebApplicationFactory` / `Testcontainers` / database references. Everything else is unit tests.
- Runner (only when targets include `net4x`): `dotnet test {project} --framework net48 --list-tests` succeeding means a runner exists
- Baseline: `dotnet build {test-project}`. On failure, ask: fix first, or proceed?

**Ask once**, in a single prompt:

1. Projects to include (only when several were found and scope is class or wider)
2. Gap fill: `auto` / `pause` for approval per class / `no`. `auto` writes tests that reflect the code as it is, not necessarily as intended; mark each with `// Auto Generated, verify expected behavior:`.
3. Quality violations: `auto` fix / `flag` only
4. Test `[Obsolete]` members? Default `no`

## Discovery

Skip for single-test and region scope. Present all findings before the sweep loop starts.

1. **Partial classes**: group every file of the same logical class
2. **Skipped files**: files without test methods, and helpers (`*Mother`, `*Serializer`, `*Fixture`, `*Extensions`, `*Builder`, `*Helper`). List them so the user can override.
3. **Analyzers**: each companion names its analyzer package. Propose adding any that is missing; the build output is the audit for `async void`, assertion misuse, and framework pitfalls.
4. **Project hygiene** (table `| Project | Issue |`): `<IsPackable>false</IsPackable>` present, current framework major version
5. **Theory serializers** _(xUnit)_: every `TheoryData<T>` of a non-primitive type has `[assembly: RegisterXunitSerializer(...)]`
6. **InternalsVisibleTo**: covers the test project when `internal` members are under test; the public API is the first choice
7. **Duplicate test IDs**: `dotnet test {project} --list-tests 2>&1 | sort | uniq -d`. Each line is a duplicate display name the runner silently skips. Table `| File | Data source | Duplicate display name |`; every entry is a required fix before the sweep touches that class.
8. **Duplicate patterns**: methods within a class whose bodies differ only by literals are parameterization candidates
9. **Missing test files**: production classes without a test class join the gap-fill flow at their turn in the loop

## Sweep Loop

Directory order, then alphabetical. Formatting is left to the pre-commit hook: run no `dotnet format` or `csharpier` mid-sweep, since formatting untouched files pollutes the diff.

For each class:

1. **Dispatch**: pick companions by detected framework and mocking library. When one is missing, ask: "No skill exists for {name}. Continue with general rules only, or skip this project?"

   | Detected    | Companion                                       |
   | ----------- | ----------------------------------------------- |
   | xUnit       | [xunit-csharp](skills/xunit-csharp/SKILL.md)    |
   | NUnit       | [nunit-csharp](skills/nunit-csharp/SKILL.md)    |
   | MSTest      | [mstest-csharp](skills/mstest-csharp/SKILL.md)  |
   | NSubstitute | [nsubstitute-csharp](skills/nsubstitute-csharp/SKILL.md) |
   | Moq         | [moq-csharp](skills/moq-csharp/SKILL.md)        |
   | RhinoMocks  | [rhinomocks-csharp](skills/rhinomocks-csharp/SKILL.md) |
   | JustMock    | [justmock-csharp](skills/justmock-csharp/SKILL.md) |

   When targets include `net4x` or `netstandard`, also read [references/multiframework.md](references/multiframework.md).
2. **Apply** every item of the [General Quality Checklist](references/quality-checklist.md) and each companion's checklist. Every item, every test.
3. **Parameterize** the duplicate patterns from Discovery step 8, following the companion's theory-data pattern.
4. **Sub-agent** when the production class has ≥5 public members or the projected test file exceeds 300 lines (class scope and wider). Brief it per [agents/subagents.md](agents/subagents.md).
5. **Annotate ambiguities**: `#warning {description}` when `TreatWarningsAsErrors` is off, otherwise `// SWEEP-AMBIGUITY: {description}`. State what the code does and what it should do.
6. **Obsolete members**: when opted in, wrap each call in `#pragma warning disable CS0618` / `restore`. Otherwise leave existing tests as they are.
7. **Verify** (mandatory, also after sub-agent output): `dotnet build {test-project}`, then `dotnet test {project} --framework {highest-modern-tfm} --filter "FullyQualifiedName~{ClassName}" 2>&1 | tail -3`. Pinning the highest modern TFM runs one framework pass per class; `tail -3` keeps the pass/fail line. Fix failures automatically and surface only the unresolvable. A class is done when build and tests pass. When errors run throughout a file, restore it (`git checkout -- {file}`) and improve it in targeted edits.
8. **Batch net4x**: without a runner, collect the `--framework net48` commands for the end.

## End of Sweep

1. Per class, a prose summary: reviewed, changed, ambiguities and gaps flagged. Include classes reviewed with no change; `git status` shows the rest.
2. The batched `--framework` commands in one block, when no runner was available
3. Full `dotnet test {test-project}`
