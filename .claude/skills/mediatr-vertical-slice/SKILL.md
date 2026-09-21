---
name: mediatr-vertical-slice
description: Build or change a vertical slice, one operation from the HTTP request through validation, authorization and the handler to the data source and back to the response (a MediatR command or query returning a C# union), by interviewing the user one question at a time, then working test-first. Use when the user wants a new or changed endpoint, operation, command, query, or a new validation, authorization or persistence rule on one.
---

# Vertical slice

A **vertical slice** is one operation taken through every layer: endpoint, validation, authorization, handler, transaction, data source, and the response back. The skill builds a new slice or changes an existing one.

Anticipated stack: MediatR, FluentValidation, a C# `union` response, Vogen value objects, and ASP.NET with either controllers or minimal APIs. The survey confirms each; [PATTERN.md](PATTERN.md) describes the pattern by role. Project file names are found by search, never assumed.

Four phases, in order. Each ends on a criterion; the next starts when it holds.

## 1. Survey

Facts are yours to find; only decisions go to the user. Survey enough to ask well, then fill the **concern map** as each concern is first touched (interview or build). A row records the concern in [PATTERN.md](PATTERN.md), the project's doc page on it, one example file (the **nearest sibling** operation where possible), and any way the project differs from the pattern. PATTERN.md opens each concern with **Search** keys.

Settle up front:
- **Host style**: `[ApiController]` classes or `MapGet`/`MapPost`/`MapGroup` minimal APIs; use the project's.
- **Docs on adding operations**: search for `adding-a-command`, `new endpoint`, `how to add`, worked examples; read them. Project docs, `CLAUDE.md`, `AGENTS.md` and `README` outrank PATTERN.md.
- **The nearest sibling** operation (same entity, kind and authorization) and its tests.
- **The test commands**: one test class, one test project, the whole suite. Run a documented one to prove it works; a .NET 10+ SDK can reject `dotnet test` for xUnit v3 projects.
- **The formatters**: the ones the docs name and the ones the tooling runs (hook files, CI workflows, a local tool manifest). List each; run each before finishing. Note any file-naming or header convention an ignore file imposes on union files (copy the newest sibling's).
- **Existing value objects**, registered policies and roles, shared case types.
- **Everything that enumerates or describes operations**: an API-description snapshot, a test listing every operation, status tables, a manual request file, and every doc, README and agent-instruction file that names a sibling operation (search the sibling's name across the repo).

A concern the project lacks (no audit, no migrations, no rate limiting) is marked **absent** and its questions are skipped. When the work changes an existing slice, follow **Changing an existing slice** below instead of the new-operation build.

**Done** when host style, nearest sibling, test commands and formatter are known and one test command has run.

## 2. Interview

Read [QUESTIONS.md](QUESTIONS.md). Ask **one question at a time** and wait for the answer.

- Use `AskUserQuestion` when available (2 to 4 options, recommended first, labelled `(Recommended)`); otherwise a numbered list with the recommendation marked.
- Open each question with one plain sentence on what it decides. Give each option its consequence in one sentence.
- Ground each recommendation in the concern map, with the reason in one line. "You decide" or "not sure" takes the recommendation, recorded as such.
- Skip a question that earlier answers settle and say what you inferred.

**Done** when the spec is written and the user confirms it. A requested change reopens only the questions it touches.

## 3. Build test-first

Follow the project's `tdd` skill when it exists; otherwise this section. The seams confirmed in the spec: HTTP, handler, validator, union.

**Red first.** Run every test before writing the production code it needs and read the failure. Red means the behavior is missing: an assertion failed, or a route answered `404`. A wrong-reason red (a null dereference in the test, a body deserialized before the status was checked, a typo) is a broken test; fix the test and re-run until the reason is right.

**Skeleton.** A first test cannot compile without its types. Create the whole skeleton in one step, every piece a stub that compiles and does nothing useful: the command or query (with its principal parameter when it needs one), the union with its required members, an empty validator class, the request type in the API layer, the handler with its constructor dependencies returning an `Error`, the endpoint that sends the request and maps every case, and any new entity or DTO member with a neutral value. Stubs are scaffolding, so behavior is the red, never a missing type.

**Tracer bullet.** One HTTP test for the happy path through the real host, written first. Assert the status before reading any body. It is red for one reason after another (no route, then a stub handler's `500`, then a wrong body) and goes green only when the handler and endpoint both do their work.

**Vertical slices.** One slice is one outcome through every layer it touches; never write all the tests first.
1. Order by the handler's check order (validation, not found, authorization, version, uniqueness), then commit-failure mappings, then audit. The handler's order wins over the order in the spec.
2. Write the test at the innermost seam that owns the behavior (validator rule, handler branch, union classification, value-object invariant), copying the nearest sibling's style: its test layout and also how it does layer-specific work (for instance how it reads claims, and which types the layer may reference; the architecture tests fail a forbidden reference).
3. Red, minimum code, green, run that test class. Reds that are independent may share one run; implement one behavior per red.
4. Then the HTTP test for the outcome's status: red, minimal endpoint arm, green.

**Where one red per slice is not achievable:**
- **Compiler-forced union and audit members** (`ShouldCommit`, `FromCommitFailure`, `FromValidationErrors`, `FromNotAuthorized`, `DescribeAudit`) exist from the skeleton. Write their tests, then prove each by **mutation**: flip one arm, watch the test fail, revert.
- **A shared helper** that decides several outcomes at once (load, owner check, version check): write the tests for all of them, confirm each red against the stub, then wire the helper; they go green together.
- **HTTP tests sharing one endpoint**: after the tracer, the remaining status tests may be red as a group and go green as their arms land. A not-found HTTP test asserts the problem `code`, since an unmapped route also answers `404`.
- **Tests that arrive green because an earlier slice already implemented the behavior** (typical for HTTP tests over a read handler): prove each by **mutation**. Break the exact line that should make it fail (drop a filter, flip a sort, delete a rule), watch the test fail, revert, and record which mutation covered which test.
- **Persistence**: when the ORM's conventions already map a new member, a round trip is green at once; get a red from a model-metadata test (required, default, index), or by mutation.

**Ripples.** A new member on a shared DTO or entity breaks every construction of it, including target-typed `new(...)` in test projects. Prefer a required member over a default that leaks into the API contract, and expect the contract snapshot to change. After the tracer is green, run the whole test project of each layer touched (endpoint-enumerating tests fail only there), not just the class.

**Idempotent domain mutations**: an operation on a value already in the target state returns it unchanged; the version advances only when state changes.

A new union case breaks every exhaustive `switch` over it; fix each with the smallest correct arm. Refactoring waits for the review stage, outside this loop.

**Done** when every behavior has a test seen red (or proven by mutation) then green; authorization has the allowed caller and each refused kind; a failing case of a transactional command is shown to leave nothing behind; the tracer bullet is green.

## Changing an existing slice

For a change to a rule, a field or a status on an operation that already exists (no new operation), read its files and their tests, interview only the decisions the change touches, and skip what the change cannot touch: no skeleton, no tracer bullet from scratch, and no concern-map rows or checklist lines for concerns it never reaches.

1. **Find every consumer.** Search for the rule, DTO member or helper being changed. A shared rule reaches every operation that uses it; ask (question 10b) which of them the change covers.
2. **Find what the change would now break.** Search fixtures, seed data, existing tests and examples for values the new rule rejects, and search docs and XML doc comments for the old rule's wording and numbers (`at most 200`, `non-empty`).
3. **Test through each consumer.** A shared rule is wired separately by each consumer, so test it at the seam that owns it in each one (a validator test per consumer). Add an HTTP test for a consumer when no existing HTTP test already exercises that outcome for it; extend an existing test's rows when one does, and say which you chose.
4. **Red as usual.** The tracer is the existing HTTP test for the affected behavior, extended until it fails for the right reason. Boundary cases that already pass (the old rule accepted them) are proven by mutation.
5. **Done** when each consumer's new behavior has a test seen red or proven by mutation, the tests around it are still green, and the sweep in step 2 found and updated every statement of the old rule.

## 4. Finish

Work the **Finish** list in [PATTERN.md](PATTERN.md); each item is done or recorded as not applicable with the reason. Regenerate contract snapshots by the project's documented method. Update every doc, README, agent-instruction file and doc comment that states what changed, so each states the present state. Search the sibling's name, prose counts (`all six`, `four mutations`, `the five endpoints`), tables that enumerate operations, routes or headers, and the old rule's wording and numbers. Run the project's documented formatter (when two are documented, run both, restoring local tools first), then the whole suite once.

**Done** when the full suite is green. Report files created and touched, concerns marked absent, and anything deferred. Commit only when asked.
