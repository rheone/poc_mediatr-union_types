---
name: mediatr-vertical-slice
description: Build or change a vertical slice, one operation from the HTTP request through validation, authorization and the handler to the data source and back to the response (a MediatR command or query returning a C# union), by interviewing the user one question at a time, then working test-first. Use when the user wants a new or changed endpoint, operation, command, query, or a new validation, authorization or persistence rule on one.
---

# Vertical slice

A **vertical slice** is one operation taken through every layer: endpoint, validation, authorization, handler, transaction, data source, and the response back. The skill builds a new slice or changes an existing one.

Anticipated stack: MediatR, FluentValidation, a C# `union` response, Vogen value objects, and ASP.NET with either controllers or minimal APIs. The survey confirms each; [PATTERN.md](PATTERN.md) describes the pattern by role. Project file names are found by search, never assumed.

Four phases, in order. Each ends on a criterion; the next starts when it holds.

## 1. Survey

Facts are yours to find; only decisions go to the user.

Build a **concern map**, one row per concern in [PATTERN.md](PATTERN.md): union, request, validation, handler, authorization, transaction, audit, HTTP layer, value objects, persistence, tests. Each row records the project's doc page on it, one example file (the **nearest sibling** operation where possible), and any way the project differs from the pattern. PATTERN.md opens each concern with **Search** keys.

Also settle:
- **Host style**: `[ApiController]` classes or `MapGet`/`MapPost`/`MapGroup` minimal APIs; use the project's.
- **Docs on adding operations**: search for `adding-a-command`, `new endpoint`, `how to add`, worked examples; read them. Project docs, `CLAUDE.md`, `AGENTS.md` and `README` outrank PATTERN.md.
- **Existing value objects** and the primitives they wrap.
- **Registered policies, roles and shared case types.**
- **The test commands**: one test class, and the whole suite. Run the documented one to prove it works; a .NET 10+ SDK can reject `dotnet test` for xUnit v3 projects.
- **Contract and doc artifacts that list endpoints**: an API-description snapshot test, status tables, a manual request file.

A concern the project lacks (no audit, no migrations, no rate limiting) is marked **absent** and its questions are skipped.

When the work changes an existing slice, its files are the starting point: read them and their tests, and interview only the decisions the change touches.

**Done** when every concern has a map row (or **absent**) and the test command has run.

## 2. Interview

Read [QUESTIONS.md](QUESTIONS.md). Ask **one question at a time** and wait for the answer.

- Use `AskUserQuestion` when available (2 to 4 options, recommended first, labelled `(Recommended)`); otherwise a numbered list with the recommendation marked.
- Open each question with one plain sentence on what it decides. Give each option its consequence in one sentence.
- Ground each recommendation in the concern map, with the reason in one line. "You decide" or "not sure" takes the recommendation, recorded as such.
- Skip a question that earlier answers settle and say what you inferred.

**Done** when the spec is written and the user confirms it. A requested change reopens only the questions it touches.

## 3. Build test-first

Follow the project's `tdd` skill when it exists; otherwise this section. The seams confirmed in the spec: HTTP, handler, validator, union.

- **Red first.** Run every test before writing production code and read the failure. It must be missing behavior (an assertion, a `404` at the route), not a typo. A missing type is red once the smallest stub compiles and the test then fails on behavior.
- **Tracer bullet.** One HTTP test for the happy path through the real host, written first. It stays red until the last slice it needs.
- **Vertical slices.** One slice is one outcome through every layer it touches. Never write all the tests first.
  1. Order: happy path; each failure in handler-check order (validation, not found, authorization, version, uniqueness); commit-failure mappings; audit.
  2. Write the test at the innermost seam that owns the behavior: a validator rule, a handler branch, a union classification, a value object invariant, then the HTTP status. Copy the seam's style from the nearest sibling's tests.
  3. Red, minimum code, green, run that test class.
- A new union case breaks every exhaustive `switch` over it. Those compiler errors are the slice's to-do list; fix each with the smallest correct arm.
- Refactoring waits for the review stage, outside this loop.

**Done** when every union case has a test seen red then green; authorization has the allowed caller and each refused kind; a failing case of a transactional command is shown to leave nothing behind; the tracer bullet is green.

## 4. Finish

Work the **Finish** list in [PATTERN.md](PATTERN.md); each item is done or recorded as not applicable with the reason. Regenerate contract snapshots by the project's documented method, then run the whole suite once.

**Done** when the full suite is green. Report files created and touched, concerns marked absent, and anything deferred. Commit only when asked.
