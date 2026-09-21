# The pattern

Each operation is a MediatR request whose response is a C# `union` of exactly the outcomes it can produce. Expected outcomes are returned; exceptions are for unexpected ones. Validation is FluentValidation, value objects are Vogen, the host is ASP.NET (controllers or minimal APIs). Names below are those of the project this skill was written in; in another project map by role, and where the project's docs or code differ, they win.

Every concern opens with **Search** keys for the survey's concern map.

Contents: [Roles](#roles) · [Union](#union) · [Request](#request) · [Validation](#validation) · [Handler](#handler) · [Authorization](#authorization) · [Transaction](#transaction) · [Audit](#audit) · [HTTP layer](#http-layer) · [Value objects](#value-objects) · [Persistence](#persistence) · [Tests](#tests) · [Also consider](#also-consider) · [Finish](#finish)

## Roles

Everything for one operation sits in one application-layer folder, plus one endpoint. Handlers and validators are found by assembly scanning; nothing is registered by hand. Work inside-out: domain and persistence, application, then HTTP.

| Role | Purpose |
| --- | --- |
| Union `{Operation}Result` | The closed set of outcomes; the contract the rest satisfies |
| `{Operation}Command` / `Query` | A `sealed record` of raw inputs plus a marker interface |
| `{Operation}Validator` | Input-shape rules |
| `{Operation}Handler` | The behavior; returns a case |
| Request type | The HTTP body or query-string type, in the API layer |
| Endpoint | Builds the command, sends it, `switch`es on the result |

## Union

**Search:** `union `, `FromValidationErrors`, `ShouldCommit`, `Common/Results`.

| Situation | Case type | Status |
| --- | --- | --- |
| Worked, with a body | the DTO | 200 / 201 |
| Worked, no body | `Success` | 204 |
| The subject does not exist | `NotFound<TId>` | 404 |
| Malformed input, per-field | `ValidationErrors` | 400 |
| Malformed input, one field | `Error` with a validation code | 400 |
| Uniqueness broken | `Conflict` | 409 |
| Caller not allowed | `NotAuthorized` | 403 |
| Stale version | `PreconditionFailed` | 412 |
| Unexpected | `Error` | 500 unless the code is mapped |

- A case type is meaning-free; the declaring union decides commit or rollback.
- Declare only reachable cases: the endpoint must handle every case, so an unreachable one is dead code and a false promise in the API contract.
- Reuse a shared case type; add one only when the data it carries differs (a plain immutable record with an arm in every `switch` that sees it).
- A formatter may ignore union files by a naming pattern (`*Result.cs`); keep the suffix.

Each marker on the request demands members on the union; until they exist the file does not compile, so the compiler is the to-do list.

| Union implements | Because the request is | Member |
| --- | --- | --- |
| `IValidatable<T>` | validated | `FromValidationErrors` |
| `IAuthorizable<T>` | refused by policy or in the handler | `FromNotAuthorized` |
| `ITransactionOutcome<T>` | transactional | `ShouldCommit`, exhaustive over its own cases |
| `ICommitFailable<T>` | transactional | `FromCommitFailure`, exhaustive over the commit failures |

## Request

**Search:** `IQuery<`, `ICommand<`, `ITransactionalCommand<`, `IRequiresAuthorization`, `IAuditableRequest<`.

| Marker | Effect |
| --- | --- |
| `IQuery<T>` | read-only, no transaction |
| `ICommand<T>` | mutating, no transaction |
| `ITransactionalCommand<T>` | wrapped in a transaction; the union decides commit |
| `IRequiresAuthorization` | a role or group policy runs in the pipeline; exposes `Principal`, `PolicyName` |
| `IAuditableRequest<T>` | opts into the audit stream |

- Parameters are raw inputs (`Guid`, `string`, `decimal`); the handler converts to value objects after validation.
- Carry the principal only when something uses it: a role check, an ownership or membership check, recording a creator, the audit event.
- Carry the expected version when the caller must prove which state they saw.
- Every public member has an XML `<summary>` (the build enforces it).

## Validation

**Search:** `AbstractValidator<`, `ValidationBehavior`, rule extension methods.

- Validate shape: not empty, maximum length, range, allowlist. State-dependent rules (taken, missing) need the database and belong in the handler.
- Share a rule several operations use as an extension member so limits cannot drift. Test a shared rule through each consumer's validator, since each wires it separately.
- Bound every input a caller can make large or expensive.
- `ValidationBehavior` short-circuits before the handler through `FromValidationErrors`. Two designs: a `ValidationErrors` case (per-field messages), or an `Error` with a validation code mapped to `400` (single input).
- Authorization runs before validation: a refused caller gets `403` even when the input is also invalid.

## Handler

**Search:** `IRequestHandler<`.

- Returns a case directly; the union's implicit conversion wraps it.
- Leaves saving to `TransactionBehavior`: it changes tracked entities and calls the repository's add or remove.
- Orders checks from least to most revealing: exists, allowed, version current, uniqueness, then change state.
- Converts raw inputs to value objects after validation.
- Reads time from the injected `TimeProvider`.
- Returns a DTO, never the entity.
- Uniqueness: the repository's existence check first, returning `Conflict`; the unique index is the race backstop.
- Every project-owned `Async` method takes `CancellationToken cancellationToken = default`; `Handle` keeps MediatR's fixed signature.

## Authorization

**Search:** `IRequiresAuthorization`, `AuthorizationBehavior`, `IAuthorizationHandler`, `AuthorizationPolicies`, `ResourceAuthorizationService`.

Every endpoint already requires an authenticated caller; this decides which ones.

**Role-based**: the caller's own claims (role, group, scope) decide. Runs in the pipeline, before validation.
1. The command carries the principal, implements `IRequiresAuthorization` and returns a registered policy name.
2. The union has `NotAuthorized` and implements `IAuthorizable<T>`.
3. A transactional union's `ShouldCommit` maps `NotAuthorized` to no commit.
4. A rule no existing policy expresses needs a new named policy registered with the authorization services.

**Resource-based**: the decision needs the loaded record: its owner, a group it belongs to, a tenant, a role that permits this action on this kind of record. Runs in the handler.
1. The command carries the principal and omits `IRequiresAuthorization`, otherwise the pipeline also runs a role check.
2. The check reasons about the entity or a small adapter exposing only what the rule needs.
3. A rule needs a requirement, a policy containing it, and a registered `IAuthorizationHandler`. An existing owner handler serves any owned resource.
4. After loading, ask the resource authorization service: `null` when allowed, a `NotAuthorized` case when refused. Return the union built with `FromNotAuthorized`.
5. Reuse a shared helper that already loads, checks and compares versions for changes to that entity.

| | Role-based | Resource-based |
| --- | --- | --- |
| Refused caller, missing record | `403` (never loaded) | `404` (loaded first) |
| Refused caller, invalid input | `403` | `400` (validation ran first) |
| Refusal and the transaction | none opened | opened, then rolled back |
| Reveals a record exists | no | yes |

- When a record's existence is sensitive, return `NotFound` to a caller who may not see it.
- Several handlers for one requirement: any one succeeding is enough (owner or administrator). Several requirements on one policy: all must succeed.
- Reads need authorization too: gate the query, or filter in the repository query so it returns only what the caller may see. A scope taken from the caller's identity that is missing must yield an empty result or a refusal, never an unscoped query (a null owner criterion means every owner).
- An anonymous endpoint needs an explicit allow-anonymous marker and a deliberate review.
- Confirm a group or scope claim actually arrives under the mapped claim type before depending on it.

## Transaction

**Search:** `TransactionBehavior`, `ITransactionOutcome`, `ICommitFailable`, `CommitResult`.

Only `ITransactionalCommand<T>` requests are wrapped. The behavior opens a transaction, runs the handler, asks the union whether to commit, and on a failed commit rolls back and asks the union to classify the failure.

- **`ShouldCommit`**: exhaustive over the union's own cases; the success case commits, every failure does not. A catch-all arm defeats the exhaustiveness check.
- **`FromCommitFailure`**: maps each commit failure to a case of the union.

| Commit failure | Meaning | Typical mapping |
| --- | --- | --- |
| Concurrency conflict | the row changed after loading | stale-version case for an existing row; `Error` for a new row |
| Unique violation | a concurrent request took the value | `Conflict`; `Error` when uniqueness cannot be violated |

- A failure case still opens a transaction (only a validation failure skips it) and is rolled back.
- One commit per request, made by the behavior.
- Side effects that cannot roll back run after the commit, through a notification, with eventual consistency.
- A change to an existing row accepts the caller's version, passes it as the expected version, and returns the stale-version case on mismatch.

## Audit

**Search:** `IAuditableRequest`, `AuditBehavior`, `DescribeAudit`.

When the project has an audit mechanism, a command that changes security-relevant state or refuses someone who tried implements `IAuditableRequest<T>`: an action name, the caller, a failure policy and `DescribeAudit`, an exhaustive `switch` so every outcome is described.

- Best-effort cannot fail the request (the change has committed); fail-closed refuses the request when the audit write fails.
- Tokens, secrets, `Authorization` headers and bodies stay out of every event.

## HTTP layer

**Search:** `[ApiController]` or `MapGet`/`MapPost`/`MapGroup`, `ProducesResponseType` or `Produces<`, `ProblemDetails`, `ToProblemResult`, `RateLimit`.

Same rules for both host styles; the project's style decides the syntax.

- **Request type**: a `sealed record` of shape only. Fields the caller must not set (owner, id, version, timestamps) are left off, so a body cannot set them.
- **Endpoint** builds the command, sends it, `switch`es on the result. Each endpoint keeps its own `switch`, so the compiler's exhaustiveness error (`CS8509`) applies; a union that gains a case breaks the build until the arm exists. In a minimal API, give the handler a named method or local function so the `switch` is visible and testable.
- One line per arm, through the project's shared problem-result helpers.
- A write rate-limit policy on every mutating endpoint; an endpoint naming none gets the read policy. Endpoints are time-boxed by default.
- Declare every status the endpoint can return, including `401` and `500` (`[ProducesResponseType]`, or `.Produces<>()`/`.ProducesProblem()`), so the API description is complete; add the project's ETag metadata when an `ETag` is returned.
- Endpoint names end in `Async` and take `CancellationToken cancellationToken = default`, where the project's convention says so.
- Generated URLs (`Location`, `Link`) come from the project's versioned helper; a `201` returns `Location` for the new resource's read endpoint.
- Conditional requests: `PUT` and `PATCH` require the version header (absent `428`, malformed `400`, stale `412`); `DELETE` treats it as optional. Parse it before sending the command.
- An `Error` becomes `500` unless its code is registered in the error-to-status mapping; a common outcome deserves its own case type.
- Route and query values bind as primitives and convert to value objects in the handler, unless the project already binds a value object directly.

## Value objects

**Search:** `[ValueObject<`, `readonly partial struct`, a `Vogen` package reference, hand-written `ValueConverter`s.

Wrap a primitive when it is an identity (`ProductId`), a measure with an invariant (`Money`, a quantity) or a constrained string (an email, a code). A distinct type prevents swapping two values of the same primitive, and an invalid value cannot be constructed.

Choose per input: **reuse** an existing value object that wraps the same concept (a second type splits its rules); **create** one for a value with an invariant or an identity; **keep the primitive** for free text with no rule or a field used once.

A Vogen value object:
- is a `readonly partial struct` marked `[ValueObject<TPrimitive>(conversions: Conversions.SystemTextJson)]`, serializing as the bare primitive;
- holds its invariant in `private static Validation Validate(TPrimitive input)`, returning `Validation.Ok` or `Validation.Invalid("message")`;
- is built with `From(value)` (throws on invalid) or `TryFrom(value, out var result)` (does not); `Validate` runs inside every factory;
- gets a static `New()` for generated identities and operators for any ordering it needs;
- lives in the domain layer; its `Vogen` package reference is not `PrivateAssets="all"`, since generated code needs it at compile time.

Where it meets the rest:
- The command carries the primitive; the validator gives the per-field `400` first; the handler calls `From` after validation. `From` throwing in a handler means the validator and the value object disagree.
- Keep the validator's bounds and `Validate` consistent, and pin that with a test feeding both the same boundary values.
- The DTO exposes the value object; conversions serialize the primitive.
- Write the `ValueConverter` by hand in the persistence layer when the domain must stay free of an ORM reference; Vogen's generated converter would pull one in.

Tests, in the domain test project: a valid value, each invalid value with its message, equality of two instances over the same primitive, `TryFrom` both ways, the JSON round trip.

## Persistence

**Search:** the `DbContext`, `ValueConverter`, repository interfaces and implementations, unit of work, migrations folder.

- Domain behavior is a method on the entity that guards its own invariants; a mutation advances the entity's version, and only when state actually changes (a repeated mutation is a no-op).
- A new repository method needs its implementation and a test against a real database. The interface speaks in domain terms and never exposes `IQueryable`.
- Every uniqueness rule has a unique index as well as the up-front check.
- Each value object the entity stores needs a converter (see [Value objects](#value-objects)).
- Where the project uses migrations, each schema change gets one, tested against a copy of real data.
- The domain layer references no other project.

## Tests

**Search:** the test projects, `TheoryData`, test-data builders ("mother" classes), the web application factory, a routes file.

| Level | Proves |
| --- | --- |
| Handler | each case returned; the repository calls; nothing saved directly (repository substituted) |
| Validator | every rule, both sides of each boundary; null, empty, whitespace |
| Union | `ShouldCommit` per case; `FromCommitFailure` per failure; `FromValidationErrors` |
| Value object | valid, invalid, equality, `TryFrom`, JSON round trip |
| HTTP (real host, real database) | every status, the headers, the problem body, the audit event |

Also, when they apply:
- authorization from both sides: the allowed caller and each refused kind (no role, wrong role, wrong-case role, owner and non-owner, group member and non-member);
- the rollback: a failing case leaves nothing behind (check the database, not only the status);
- the race: two requests both pass the up-front check and one gets the mapped conflict;
- the audit event, including for a refusal;
- a persistence change against a real database.

Conventions: URLs come from one routes file; each test class gets its own host and database; time is fixed through the injected clock; a union is unwrapped to its concrete case for assertions; names and layout follow the nearest sibling's tests.

## Also consider

- A new response header a browser client must read is added to the exposed-headers list and any test that checks it.
- A new request media type answers `415` for others and appears in the API description.
- A list endpoint needs filtering, a sort allowlist, a maximum page size and a deterministic tiebreaker.
- A retried `POST` creates a second record unless a uniqueness rule or idempotency key prevents it.
- Work that outlives the request timeout needs another shape (accept, then poll).
- A new setting follows the project's options convention: bound, validated on start.
- Logging follows the project's structured pattern; tokens, headers and bodies are never logged.
- A problem response's message reveals no internals and no records the caller may not know exist.
- Adding an endpoint is additive; changing what a shipped endpoint returns needs a new version.
- Personal data stays out of logs, audit events, error messages and API examples.

## Finish

Each item is done or recorded as not applicable with the reason.

- [ ] The union holds only reachable cases and documents each one's meaning
- [ ] The union implements every interface its request's markers require
- [ ] `ShouldCommit` and `FromCommitFailure` classify every case and failure, with no catch-all
- [ ] Authorization is decided, refusals map to `403`, reads are covered
- [ ] The handler returns cases, never commits, returns a DTO
- [ ] Every input is bounded; nothing the caller must not set is on the request type
- [ ] Value objects are reused or created deliberately, each with tests, and validators agree with their invariants
- [ ] Persistence changes have a constraint, a converter and, where used, a migration
- [ ] The endpoint has its own `switch`, a rate-limit policy, every status declared, the project's naming and token conventions
- [ ] Tests at every applicable level, including the refused caller, the rollback and the race
- [ ] Contract snapshot regenerated and its diff reviewed
- [ ] Doc tables, the manual request file and READMEs that list endpoints updated
- [ ] The whole suite is green; the formatter has been run
