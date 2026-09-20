# What this pattern provides, and its actual scope

Part of the [documentation](index.md).

> [!NOTE]
> **This is a convention, not a MediatR requirement.** `IRequestHandler<TRequest, TResponse>`
> places no constraint on `TResponse` beyond being a type — a handler is free to return a plain
> DTO, a `bool`, a `Task` with nothing meaningful in it, or anything else, and MediatR is
> completely indifferent to unions. Returning a `union` of shared and bespoke case types is
> *this repo's own deliberate convention* for expressing "exactly one of N meaningful outcomes,"
> adopted because it fits CQRS-style commands/queries that can genuinely end several different
> ways — not something MediatR asks for, and not the only valid return shape for a handler even
> within this codebase's own pattern. A trivial operation with only one possible outcome has no
> reason to introduce a union at all.

## Union types as responses

- **Exhaustiveness is enforced by the compiler.** Add a new case to a union, and every `switch`
  over it stops compiling until you handle the new case. A forgotten `if (result == null)` check
  simply can't happen — there's no null, only the cases you declared.
- **The signature *is* the contract.** `Task<CreateProductResult>` where
  `CreateProductResult` is `union(ProductDto, ValidationErrors, Error, Conflict)` tells a caller everything
  that can happen without reading the method body or any docs.
- **Mix-and-match per operation.** A union is declared per use case, not shared globally — a
  "get" query might only ever produce `(Dto, NotFound, Error)` while a "delete" produces
  `(Success, NotFound, Error, NotAuthorized, PreconditionFailed)`. No forcing every endpoint through one bloated `Result` type with
  irrelevant properties.
- **No boxing tax for the common path.** Case types are checked directly against the union's
  underlying `object? Value` at pattern-match sites; the union itself is a lightweight struct.
- **Generic code can still build a union it doesn't know the shape of.** By having each union
  implement an interface with a **static abstract member** (`static abstract TSelf
  FromValidationErrors(ValidationErrors errors)`), a fully generic `ValidationBehavior<TRequest,
  TResponse>` can construct the right concrete union's error case without ever naming it — see
  [`IValidatable<TSelf>`](../src/MediatrUnionPoc.Application/Common/Abstractions/IValidatable.cs) and
  [Static abstract interface members](union-type.md#static-abstract-interface-members-why-generic-code-can-build-a-union-its-never-seen)
  below.

# What this POC demonstrates, and what it leaves out

**Demonstrated**, all on one small Products API:

- `union` response types for every command and query, with compiler-checked `switch` exhaustiveness
  at the controller and at every per-union hook (`ShouldCommit`, `FromCommitFailure`,
  `FromValidationErrors`, `FromNotAuthorized`).
- A MediatR pipeline (logging, audit, authorization, validation, transaction) that stays generic through
  `static abstract` interface members.
- Optimistic concurrency with a weak `ETag` and `If-Match`, duplicate-name `Conflict`, JSON Merge
  Patch, filtered/sorted/paged listing with `X-Total-Count` and `Link` headers.
- Uniform RFC 7807 problem bodies, a trace id on every response and log line, and a global
  exception handler for the genuinely unexpected.

**Deliberately out of scope** (this is a pattern POC, not a production template):

- **An identity provider.** The API validates JWT bearer tokens but issues none (no login, no
  refresh, no user store); see [Where the identity comes from](authorization.md#where-the-identity-comes-from).
- **Cross-instance rate limiting.** Limits are counted per instance, in memory; enforcing one limit across
  instances needs a gateway or a shared store (see [Rate limiting](operations.md#rate-limiting-a-budget-per-caller)).
- **Migrations and a production database.** The schema is created with `EnsureCreated` on SQLite.
  There is no migration history, and the unique-violation detection reads SQLite's error message,
  so another provider needs its own check (see
  [Duplicate product names](concurrency.md#duplicate-product-names)).
- **Soft delete and audit columns.** `DELETE` removes the row; the only timestamp is `CreatedAt`. (Who did
  what is recorded in the separate [audit stream](audit.md#audit-stream-a-separate-record-of-security-relevant-actions),
  not in the product table; an audit database table is not built.)
- **Exception tracking and a log server.** Serilog writes the console and a rolling JSON file; no
  exception-tracking service or log server (Seq) is bundled. The trace id is the join key for whichever
  you add: OpenTelemetry (exception events on spans), Sentry, or a Serilog sink added in
  configuration. See [Trace id and unhandled exceptions](logging-and-errors.md#trace-id-and-unhandled-exceptions).
