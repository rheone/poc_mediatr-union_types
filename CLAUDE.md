# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A proof of concept testing whether C#'s `union` type (a C# 15 / .NET 11 preview feature) works as a MediatR/CQRS response type, replacing thrown exceptions and null with a closed, compiler-exhaustive set of outcomes per operation. Design rationale, the HTTP contract, security and operations live in `docs/` (start at [`docs/index.md`](docs/index.md)); `README.md` has the motivation. Each section below ends in a pointer: follow it before changing that area.

## Commands

```bash
dotnet build                                    # build the whole solution
dotnet test                                     # run all tests
dotnet test --filter "FullyQualifiedName~Name"  # one test/class by (partial) name
dotnet run --project src/MediatrUnionPoc.Api    # run the API (http://localhost:5233)
dotnet format whitespace                        # fix whitespace/using-order issues
```

- Persistence is SQLite everywhere. Without `ConnectionStrings:Products` the API uses a private in-memory database (empty each start, `EnsureCreated`); `Data Source=products.db` persists. No EF Core InMemory provider exists anywhere.
- `global.json` pins an exact .NET 11 preview SDK. Without it, Visual Studio falls back to the newest stable SDK and fails with `NETSDK1045`.

## Architecture

Four `src/` projects, each with a same-named unit test project under `tests/` (plus `*.IntegrationTests` where a real database or HTTP host is needed):

- **Domain**: `Product`, Vogen value objects, `IProductRepository`/`IUnitOfWork`. References no other project. `IUnitOfWork.CommitAsync` returns the `CommitResult` union (`Committed | ConcurrencyConflict | UniqueViolation`).
- **Application**: MediatR commands/queries/handlers/validators as vertical slices under `Features/<Area>/<Operation>/`; `Common/` holds the pipeline machinery. No JWT dependency.
- **Infrastructure**: EF Core. The `ValueConverter`s for the Vogen types are hand-written, not Vogen's generated ones, so Domain stays free of an EF Core reference.
- **Api**: controllers whose only job is to `switch` on the returned union and produce an `IActionResult`. Repeated failure arms are one-line extension members in `Api/Http/` (`error.ToProblemResult(HttpContext)`), driven by the DI-registered `HttpMappingOptions`. Each controller keeps its own `switch` so `CS8509` exhaustiveness applies.

`tests/MediatrUnionPoc.ArchitectureTests` (NetArchTest) enforces the layering, and `DocumentationLinkTests` requires every relative link, `#fragment` (GitHub heading slug) and footnote in every `*.md` (except `.claude/skills`, `docs/research`, build output) to resolve. Renaming a heading or page means fixing the links to it, or the test fails with `file:line -> target`.

`tests/CompileTimeChecks/` holds four scratch projects **deliberately excluded from the `.slnx`**: they prove a non-exhaustive `switch` is a real `CS8509` error, driven by `ExhaustivenessTests.cs` shelling out to `dotnet build`. Read its `README.md` before adding a probe.

### The core pattern

Every command/query returns a `union` of exactly the outcomes that operation can produce and never throws for an expected outcome. The controller `switch` is the only place a union becomes an HTTP status.

Case types (`Success`, `NotFound`, `Error`, `ValidationErrors`, `Failure`, `NotAuthorized`, `PreconditionFailed`, `Conflict`, in `Application/Common/Results/`) are meaning-free and shared. A case type's identity never implies commit/rollback or anything else: the declaring union decides. That is why `ITransactionOutcome.ShouldCommit` exists rather than `TransactionBehavior` matching known case types.

Two status quirks: a successful `PUT` returns its `ProductDto` case as `204` (with the new `ETag`), `PATCH` as `200`. `GetById`/`Delete` fold validation failures into `Error(ValidationFailureCode)`; `GetPaged`/`Create`/`Update`/`Patch` have a real `ValidationErrors` case. `Failure` is defined but no Products union declares it.

See [`docs/no-exceptions.md`](docs/no-exceptions.md), [`docs/case-types.md`](docs/case-types.md), [`docs/http-contract.md`](docs/http-contract.md) (per-endpoint status table), [`docs/adding-a-command.md`](docs/adding-a-command.md).

### MediatR pipeline

Registered in `Application/DependencyInjection.cs` in this exact order: `LoggingBehavior` → `AuditBehavior` → `AuthorizationBehavior` → `ValidationBehavior` → `TransactionBehavior`. Who is calling is checked before whether input is well-formed, and audit sits outside both so a refused request is still recorded. Marker interfaces in `Application/Common/Abstractions/` select the behaviors:

| Marker | Effect; what the response union must implement |
|---|---|
| `ICommand<T>` / `IQuery<T>` | Mutating / read-only; neither is wrapped in a transaction |
| `ITransactionalCommand<T>` | Opts into `TransactionBehavior`: union implements `ITransactionOutcome<T>` (`static abstract bool ShouldCommit`, an exhaustive `switch` over its own cases) and `ICommitFailable<T>` (`FromCommitFailure`, an exhaustive `switch` over `ConcurrencyConflict`/`UniqueViolation`; the compiler forces every transactional union to classify every commit failure). On failure the behavior rolls back and returns `TResponse.FromCommitFailure(failure)` |
| `IValidatable<T>` | `FromValidationErrors`; lets `ValidationBehavior` short-circuit generically (all six operations) |
| `IRequiresAuthorization` | Request exposes `Principal` and `PolicyName`; union implements `IAuthorizable<T>` (`FromNotAuthorized`). Only Delete (`Administrator`) and IssueImpersonationToken (`Impersonator`) use it; Update/Patch check ownership inside the handler (`LoadForChangeAsync` → `ResourceAuthorizationService`) because that needs the loaded product |
| `IAuditableRequest<T>` | Opts into the audit stream (`AuditAction`, `AuditFailurePolicy`, `AuditPrincipal`, `DescribeAudit`). Used by the four product mutations (`BestEffort`) and IssueImpersonationToken (`FailClosed`) |

`LoggingBehavior` applies to every request. Transactional commands are Create, Update, Patch, Delete.

See [`docs/request-lifecycle.md`](docs/request-lifecycle.md), [`docs/transactions.md`](docs/transactions.md), [`docs/authorization.md`](docs/authorization.md).

### Domain rules that span files

- **Unique names**: two products may not share a name ignoring case and surrounding whitespace, globally. `Domain/ProductNames.Normalize` is the one definition; `Product.NormalizedName` carries it. Create/Update/Patch check up front via `IProductRepository.ExistsWithNameAsync(name, excludingId)` → `Conflict`; the unique index is the race backstop, surfaced as `UniqueViolation` and mapped to the same `Conflict`. Delete has no `Conflict` case.
- **Optimistic concurrency**: `ProductVersion` (advanced by the Domain on every mutation) is a weak `ETag` (`W/"n"`). `PUT`/`PATCH` require `If-Match` (absent 428, malformed 400, stale 412); `DELETE` treats it as optional. `IfMatchHeader.Parse` classifies it before any command is sent. See [`docs/concurrency.md`](docs/concurrency.md).
- **Patch** is RFC 7396 JSON Merge Patch: `Optional<T>` (`Common/Optional.cs`, absent at `default`) bound by `OptionalJsonConverterFactory`, `Content-Type: application/merge-patch+json` (else 415), unknown members ignored, a present `null` or an empty patch is a 400. The handler shares `LoadForChangeAsync` with Update and the name/price rules (`ProductRuleExtensions`) with Create/Update. See [`docs/patch.md`](docs/patch.md).
- **Listing**: the Domain owns a database-agnostic vocabulary (`ProductCriteria`, `ProductSort` with an enum allowlist, `PagedResult<T>`, the one place page arithmetic lives); only `ProductRepository` turns it into a query, every sort ending in an implicit `Id` tiebreaker. `CreatedAt` is stamped from an injectable `TimeProvider` and stored as UTC ticks (`UtcTicksValueConverter`) because SQLite cannot order a `DateTimeOffset`'s text. See [`docs/listing.md`](docs/listing.md).

### API host: gotchas

Each area has a page; these are the points a change is most likely to break.

- **Authentication** (`Api/Authentication/`): `MapInboundClaims` is deliberately `true` so `sub`/`role` reach Application as `ClaimTypes.NameIdentifier`/`ClaimTypes.Role`; do not turn it off. A fallback policy requires an authenticated user unless `.AllowAnonymous()`. Outside Development the host refuses to start without `Authentication:Jwt:SigningKey`. Integration tests use a header-driven scheme (`client.AsUser("alice", roles)`); `ApiAuthentication.RealJwt` plus `TestData/JwtTestTokens` give real signed tokens. See [`docs/authorization.md`](docs/authorization.md#where-the-identity-comes-from).
- **Options convention for every new setting**: `AddOptions<T>().BindConfiguration("Section").ValidateOnStart()` plus an `[OptionsValidator]` source-generated `IValidateOptions<T>` (DataAnnotations on the class); `HealthEndpointsOptions` is the reference. Nullable list options exist because the binder appends to arrays instead of replacing. See [`docs/operations.md`](docs/operations.md).
- **Impersonation** ([`docs/impersonation.md`](docs/impersonation.md)): an authentication bypass available in every environment, signed with a separate `Impersonation:SigningKey` that must differ from the JWT key; the disabled outcome is an `Error` coded `IMPERSONATION_DISABLED` mapped to 404. Application reaches it only through `IImpersonationTokenIssuer`.
- **Audit** ([`docs/audit.md`](docs/audit.md)): a separate append-only JSON Lines stream, never Serilog, never sampled. `AddApplication` registers `AuditBehavior` but no `IAuditLog`; the host registers it (`AddAudit()`, after `AddApplication()`). `BestEffort` cannot fail the request because `TransactionBehavior` is inside `AuditBehavior` and the change has already committed. Never put a token, secret, `Authorization` header or body in an event. Tests get a private temp audit directory per `ProductsApiFactory` (`ReadAuditEvents()`).
- **Versioning** ([`docs/http-contract.md`](docs/http-contract.md#api-versioning)): URL segment, `Asp.Versioning`; `Location` and `Link` are always the canonical `/api/v1/...` URL. The unversioned `/api/...` alias is transitional and absent from OpenAPI. `Asp.Versioning.OpenApi` cannot be used (needs `Microsoft.OpenApi` 2.x, the built-in generator is 3.x). Domain/Application/Infrastructure never reference `Asp.Versioning`. Integration tests take every URL from `ApiRoutes.cs`.
- **Rate limiting, timeouts, CORS, health** ([`docs/operations.md`](docs/operations.md)): every controller action is limited and time-boxed by default (a test walks every endpoint to enforce rate limiting); only an explicit `DisableRateLimiting()`/`DisableRequestTimeout()` exempts. Middleware order is load-bearing (forwarded headers, trace id, request logging, exception handler, status-code pages, HTTPS redirection, CORS, authentication, user log context, impersonation audit, request timeout, rate limiter, authorization, endpoints). The default test host uses maximal limits and a 10 minute timeout; use `factory.WithLimits(...)` / `factory.WithTimeouts(...)`. Test hosts run in Development: post-configure `ApiCorsOptions` to pin origins.
- **Trace id, errors, logging** ([`docs/logging-and-errors.md`](docs/logging-and-errors.md)): `HttpContext.TraceId` is the one accessor. `ILogger` is the only logging API (Serilog is wired behind it in the Api host, configured by the `Serilog` section, not `Logging:LogLevel`). Hot-path messages are `[LoggerMessage]` methods with stable event ids; never log tokens, `Authorization` headers or bodies. Tests read events from `ProductsApiFactory.LogSink`.
- **OpenAPI contract check**: `OpenApiContractTests` diffs the served `/openapi/v1.json` against `tests/MediatrUnionPoc.Api.IntegrationTests/Contracts/openapi.v1.json`. Regenerate for a deliberate change with `UPDATE_OPENAPI_SNAPSHOT=1 dotnet test ... --filter "FullyQualifiedName~OpenApiContractTests"` (refused when `CI` is set) and commit the diff. See [`docs/operations.md`](docs/operations.md#openapi-contract-check-no-accidental-drift).

## Conventions

- Every async method this repo owns the signature of ends in `Async` and takes `CancellationToken cancellationToken = default`. The exception is `Handle` on MediatR's `IRequestHandler`/`IPipelineBehavior`, whose name and non-defaulted token are fixed.
- `SuppressAsyncSuffixInActionNames` is set to `false` in `Program.cs` so action names keep `Async`; otherwise `nameof(GetByIdAsync)` in `Url.Action` silently stops matching.
- Central Package Management: versions live in `Directory.Packages.props`, never in a `.csproj`. The health-check package is pinned to EF Core's `10.0.12`.
- `.editorconfig` documents every analyzer severity override with rationale; check it before treating a suppressed rule as an oversight. `SA1649` is disabled because it crashes on any file containing a `union`.
- Every public/protected member needs an XML `<summary>` (`CS1591` is enforced repo-wide except `CompileTimeChecks`).
- When writing or updating:
  - markdown (*.md) files use the skills `.claude\skills\github-markdown` and `.claude\skills\mermaid-diagram-generator` for diagrams
  - code documentation use skill `.claude\skills\csharp-docs-and-comments` with the current state and not including deltas
  - tests use the skills `.claude\skills\csharp-test-sweep` and `.claude\skills\tdd`
  - creating or updating a plan use the skill `.claude\skills\grilling`

<!-- CODEGRAPH_START -->
## CodeGraph

In repositories indexed by CodeGraph (a `.codegraph/` directory exists at the repo root), reach for it BEFORE grep/find or reading files when you need to understand or locate code:

- **MCP tool** (when available): `codegraph_explore` answers most code questions in one call — the relevant symbols' verbatim source plus the call paths between them, including dynamic-dispatch hops grep can't follow. Name a file or symbol in the query to read its current line-numbered source. If it's listed but deferred, load it by name via tool search.
- **Shell** (always works): `codegraph explore "<symbol names or question>"` prints the same output.

If there is no `.codegraph/` directory, skip CodeGraph entirely — indexing is the user's decision.
<!-- CODEGRAPH_END -->
