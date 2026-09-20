# CLAUDE.md

This file provides guidance to AI coding assistants working with code in this repository.

## What this is

A proof of concept testing whether C#'s `union` type (a C# 15 / .NET 11 preview language feature) works as a MediatR/CQRS response type, replacing
thrown exceptions and null with a closed, compiler-exhaustive set of outcomes per operation. See
`README.md` for the full write-up (motivation, design rationale, Mermaid diagrams) — this file only
covers what a session needs to be productive quickly.

## Commands

```bash
dotnet build                                    # build the whole solution
dotnet test                                     # run all tests
dotnet test --filter "FullyQualifiedName~Name"  # run a single test/class by (partial) name
dotnet run --project src/MediatrUnionPoc.Api    # run the API locally (http://localhost:5233)
dotnet format whitespace                        # auto-fix whitespace/using-order issues
```

Persistence is SQLite everywhere. With no `ConnectionStrings:Products` value the API keeps a private
in-memory database (empty on every start, schema via `EnsureCreated`); set it to e.g.
`Data Source=products.db` to persist. There is no EF Core InMemory provider anywhere.

An isolated Linux dev container is defined in `.devcontainer/` (a base image plus published and local Dev Container Features, no Dockerfile; open it with *Dev Containers: Clone Repository in Container Volume*; see
`docs/DevContainer.md`). Inside it `rg` (ripgrep), `gh`, the pinned SDK, `csharp-ls` and the `codebase-memory-mcp` code-graph server are
installed; egress is default-deny, so a host that is not in `.devcontainer/features/egress-firewall/allowed-hosts.txt` is unreachable. `.devcontainer/smoke-test.sh`
checks the whole setup.

The SDK is pinned via `global.json` to an exact .NET 11 preview build (`allowPrerelease: true`).
Without it, some tools (Visual Studio in particular) fall back to the newest *stable* SDK on the
machine and fail with `NETSDK1045`.

## Architecture

**Solution layout** (`MediatrUnionPoc.slnx`, 4 `src/` projects + 5 `tests/` projects):

- `src/MediatrUnionPoc.Domain` — `Product` entity, Vogen value objects (`ProductId`, `Money`,
  `ProductVersion`), `IProductRepository`/`IUnitOfWork` interfaces. `IUnitOfWork.CommitAsync` returns
  a `CommitResult` union (`Committed | ConcurrencyConflict | UniqueViolation`). Has no dependency on any other project here.
- `src/MediatrUnionPoc.Application` — commands, queries, handlers, validators, organized as
  **vertical slices** under `Features/Products/<Operation>/` (Create, Update, Patch, Delete, GetById,
  GetPaged) and `Features/Impersonation/IssueToken/` rather than by technical layer. `Common/` holds the shared pipeline machinery (below).
  `Patch` is a JSON Merge Patch (RFC 7396) partial update: its command carries `Optional<string?>` /
  `Optional<decimal?>` (`Common/Optional.cs`, absent at `default`, present via `Optional<T>.Of`,
  serializer-free), and its handler shares the load/ownership/version steps with Update
  (`Features/Products/Common/ProductChangeExtensions.LoadForChangeAsync`) and the name/price rules
  with Create/Update (`ProductRuleExtensions`).
- `src/MediatrUnionPoc.Infrastructure` — EF Core (`EfCoreUnitOfWork`, `ProductRepository`,
  hand-written `ValueConverter`s for the Vogen types — not Vogen's own generated converter, to
  keep Domain free of an EF Core reference).
- `src/MediatrUnionPoc.Api` — two controllers (`ProductsController`, `ImpersonationController`); every action's only job is to
  `switch` on the union MediatR returns and produce an `IActionResult`. The repeated failure arms
  are one-line calls to C# 14 extension members in `Api/Http/` (`error.ToProblemResult(HttpContext)`
  etc., all RFC 7807 `application/problem+json`, the 404 with a `code: "NOT_FOUND"` member) whose
  shared policy is a DI-registered `HttpMappingOptions` (`AddResultHttpMapping(...)` in
  `Program.cs`: `Error.Code` to status table, `type` URI switch); each extension also takes per-call
  overrides, and the controller keeps its own `switch` so `CS8509` exhaustiveness still applies.
  `PATCH /api/v1/products/{id}` requires `Content-Type: application/merge-patch+json` (`[Consumes]`,
  else 415) and binds `Optional<T>` members through `OptionalJsonConverterFactory` (registered in
  `Program.cs` via `AddJsonOptions`); unknown members are ignored, a present `null` or an empty
  patch is a 400. The OpenAPI schema/media-type shaping for it lives in `Api/OpenApi/`
  (`OptionalSchemaTransformer`, `ConsumesMediaTypeTransformer`).

Each `src/` project has its own unit test project, named after it, plus a separate integration test
project wherever tests need a real database, a real HTTP host, or both:

- `tests/MediatrUnionPoc.Domain.Tests` — pure unit tests for the value objects, `Product`,
  `ProductNames`, `PagedResult` and the sort vocabulary; no dependency on any other project.
- `tests/MediatrUnionPoc.Application.Tests` — xUnit v3 + NSubstitute, organized by what's under test
  (`Unions/`, `Behaviors/`, `Handlers/`, `Validators/`, `Authorization/`, `Features/`). Handlers are
  tested against a substituted `IProductRepository`, never the real EF Core provider.
- `tests/MediatrUnionPoc.Infrastructure.IntegrationTests` — the one project that exercises the real
  EF Core SQLite provider (an in-memory database on one kept-open connection per test) end-to-end
  (`EfCoreUnitOfWorkTests`), rather than substituting `IUnitOfWork`.
- `tests/MediatrUnionPoc.Api.IntegrationTests` — boots the real ASP.NET Core host via
  `WebApplicationFactory` and exercises it through actual HTTP requests, against the real SQLite
  provider; each host gets its own private in-memory database.
- `tests/MediatrUnionPoc.ArchitectureTests` — `NetArchTest.Rules` assertions on the compiled `src/`
  assemblies enforcing the layering described above.

`tests/CompileTimeChecks/` holds four tiny scratch projects (`Exhaustive`, `NonExhaustive`,
`ShouldCommitExhaustive`, `ShouldCommitNonExhaustive`) **deliberately excluded from the `.slnx`**
— one pair proves a non-exhaustive `switch` over a union is a real `CS8509` compiler error, the
other proves the same for `ITransactionOutcome.ShouldCommit`. `ExhaustivenessTests.cs` shells out
to `dotnet build` against them; that's why those directories live outside the solution file. See
`tests/CompileTimeChecks/README.md` for why it exists and how to manage it (adding a new probe,
keeping it out of the `.slnx`, `.csharpierignore` interaction).

**The core pattern**: every command/query returns a `union` of exactly the outcomes that operation
can produce (e.g. `union CreateProductResult(ProductDto, ValidationErrors, Error, Conflict)`) and never
throws for an expected outcome (validation failure, not-found, etc.) — the controller's `switch`
is the only place a union gets translated into an HTTP status. See `README.md`'s "No exceptions for
expected outcomes" and "Shared case types are meaning-free" sections for the full rationale. The
per-endpoint status table is `README.md`'s "The HTTP contract" section; note that a successful `PUT`
returns its `ProductDto` case as `204` (with the new `ETag`), `PATCH` returns it as `200`, and
`DELETE`'s `Success` is `204`.

Unions and their cases: `CreateProductResult` (ProductDto, ValidationErrors, Error, Conflict);
`IssueImpersonationTokenResult` (ImpersonationToken, ValidationErrors, NotAuthorized, Error);
`GetProductByIdResult` (ProductDto, NotFound, Error); `GetPagedProductsResult` (PagedResult, ValidationErrors,
Error); `UpdateProductResult` and `PatchProductResult` (ProductDto, NotFound, ValidationErrors, Error,
NotAuthorized, PreconditionFailed, Conflict); `DeleteProductResult` (Success, NotFound, Error,
NotAuthorized, PreconditionFailed). `Failure` is defined but no Products union declares it.

**MediatR pipeline** (registered in `Application/DependencyInjection.cs`, in this exact order):
`LoggingBehavior` → `AuditBehavior` → `AuthorizationBehavior` → `ValidationBehavior` → `TransactionBehavior` — who's
calling is checked before whether their input is well-formed, and the audit behavior sits outside both so a refused request is still recorded. When `CommitAsync` reports a failure,
`TransactionBehavior` rolls back and returns `TResponse.FromCommitFailure(failure)`. Marker interfaces in
`Application/Common/Abstractions/` control which behaviors apply to which requests:

- `ICommand<TResponse>` — mutates state, no transaction assumption.
- `ITransactionalCommand<TResponse> : ICommand<TResponse>` — opts into `TransactionBehavior`;
  requires the response union to implement `ITransactionOutcome<TResponse>` (a `static abstract
  bool ShouldCommit(TResponse)`, implemented per-union as an exhaustive `switch` over that union's
  own cases) and `ICommitFailable<TResponse>`.
- `ICommitFailable<TSelf>` — a union implements `static abstract TSelf
  FromCommitFailure(CommitFailure)` as an exhaustive `switch` over `ConcurrencyConflict` /
  `UniqueViolation`, deciding what a refused commit means for that operation (e.g. Update:
  `ConcurrencyConflict` → `PreconditionFailed`; Create/Update/Patch: `UniqueViolation` → `Conflict`;
  Create's `ConcurrencyConflict` and Delete's `UniqueViolation` → `Error`); the compiler forces every
  transactional union to classify every commit failure.
- `IQuery<TResponse>` — read-only, never wrapped in a transaction.
- `IValidatable<TSelf>` — a union implements this (`static abstract TSelf
  FromValidationErrors(ValidationErrors)`) so `ValidationBehavior` can short-circuit generically
  without knowing the concrete union type.
- `IRequiresAuthorization` — a request implements this (exposing `ClaimsPrincipal Principal` and a
  `string PolicyName`) to opt into `AuthorizationBehavior`, which checks `Principal` against
  whichever ASP.NET Core authorization policy `PolicyName` names (`IAuthorizationService` + a
  custom `IAuthorizationHandler`). Requires the response union to implement
  `IAuthorizable<TResponse>` (a `static abstract TSelf FromNotAuthorized(NotAuthorized)`), the
  same generic short-circuit pattern `IValidatable` uses for validation. Only `DeleteProductCommand`
  (`Administrator` policy) and `IssueImpersonationTokenCommand` (`Impersonator` policy: Administrator
  or Support, the same `AdministratorRequirement`/handler with two roles) use it; `Update`/`Patch` check ownership inside the handler instead (`LoadForChangeAsync` →
  `ResourceAuthorizationService`), since that needs the loaded product. See README's
  "Authorization" section for how to configure it and gate a new command behind it.

- `IAuditableRequest<TResponse>` — a request opts into the audit stream (`AuditAction`, `AuditFailurePolicy`,
  `AuditPrincipal`, `DescribeAudit(TResponse)`); `AuditBehavior` then records one `AuditEvent` per request
  whatever came back (outcome = the union case's runtime name, target and reason from the request's own
  `DescribeAudit`, so a create reads its target id from its `ProductDto` case). Used by the four product
  mutations (`BestEffort`) and `IssueImpersonationTokenCommand` (`FailClosed`). See "Audit" below.

`LoggingBehavior` applies to every request, `AuditBehavior` to `IAuditableRequest` requests, `ValidationBehavior` to any request whose response
union is `IValidatable` (all six operations), `AuthorizationBehavior` to `IRequiresAuthorization`
requests, `TransactionBehavior` to `ITransactionalCommand` requests (Create, Update, Patch, Delete).

**Listing** (`GET /api/v1/products`): the Domain defines a database-agnostic vocabulary
(`ProductCriteria`, `ProductSort` with a `ProductSortField` enum allowlist, `PagedResult<T>` — the
one place page arithmetic lives) and only `ProductRepository` in Infrastructure turns it into a
query (every sort ends in an implicit `Id` tiebreaker). `Product.CreatedAt` is stamped by
`CreateProductHandler` from an injectable `TimeProvider` (registered by `AddApplication`) and stored
as UTC ticks (`UtcTicksValueConverter`) because SQLite cannot order a `DateTimeOffset`'s text.
`GetPagedProductsResult` has a real `ValidationErrors` case (per-field 400s); `GetById`/`Delete`
still fold validation failures into `Error(ValidationFailureCode)`. The 200 carries `X-Total-Count`
and an RFC 8288 `Link` header (`Api/Http/PagingHttpExtensions`). See README's "Listing products".

Case types (`Success`, `NotFound`, `Error`, `ValidationErrors`, `Failure`, `NotAuthorized`,
`PreconditionFailed`, `Conflict` — one file each in `Application/Common/Results/`) are deliberately meaning-free and reused across unions;
a case type's identity never implies what it means for commit/rollback or anything else — only
the union that declares it decides that. That's why `ITransactionOutcome.ShouldCommit` exists
instead of `TransactionBehavior` pattern-matching a fixed list of known case types.

**Unique product names**: two products may not share a name ignoring case and surrounding
whitespace (global, not per owner). `Domain/ProductNames.Normalize` is the one definition of that
rule; `Product.NormalizedName` carries it. `Create`/`Update`/`Patch` handlers check up front through
`IProductRepository.ExistsWithNameAsync(name, excludingId)` and return `Conflict` (409); a unique
index on `NormalizedName` is the race backstop, which `EfCoreUnitOfWork.CommitAsync` reports as
`UniqueViolation` and those three unions' `FromCommitFailure` maps to the same `Conflict`. `Delete`
has no `Conflict` case.

**Optimistic concurrency**: `ProductVersion` (advanced by the Domain on every mutation) is exposed as
a weak `ETag` (`W/"n"`). `PUT` and `PATCH` require `If-Match` (absent 428, malformed 400, stale 412);
`DELETE` treats it as optional. `IfMatchHeader.Parse` (Api) classifies the header before any command is
sent.

**Authentication** (`Api/Authentication/`): real JWT bearer (`AddJwtAuthentication()`, HS256, `Authentication:Jwt`
`Issuer`/`Audience`/`SigningKey`/`ClockSkewSeconds`, validated on start with an `[OptionsValidator]`), with
`UseAuthentication()` before `UseAuthorization()`. A fallback policy `RequireAuthenticatedUser` makes every
endpoint need a caller unless `.AllowAnonymous()` (health endpoints; in Development the OpenAPI and Scalar
endpoints); the Application layer's `AddAuthorizationCore` policies and the Api's `AddAuthorization` share one
`AuthorizationOptions`. Only `appsettings.Development.json` has a (development-only) signing key: any other
environment must supply `Authentication:Jwt:SigningKey` or the host refuses to start. `MapInboundClaims` is
deliberately `true` so `sub`/`role` reach the Application layer as `ClaimTypes.NameIdentifier`/`ClaimTypes.Role`;
do not turn it off. Controllers pass `User` into commands; a token without `sub` cannot `POST` (403 through the
`NotAuthorized` case, decided in the controller). `ProblemDetailsAuthorizationResultHandler` renders the
middleware's 401/403 as problem bodies with the trace id. `dotnet user-jwts` tokens work in Development (the
configuration adds to, not replaces, `Authentication:Schemes:Bearer`). Integration tests use a header-driven
test scheme by default (`client.AsUser("alice", roles)`, `ProductsApiFactory`) and `ApiAuthentication.RealJwt`
plus `TestData/JwtTestTokens` for real signed tokens.

**Health checks and options** (`Api/Health/`): `GET /health/live` (no checks) and `GET /health/ready`
(a `SELECT 1` round trip on `AppDbContext`, tag `ready`) are anonymous, plain-text, registered by
`AddHealthEndpoints()` / `MapHealthEndpoints()`; paths come from the `HealthEndpoints` section. With
the default private in-memory SQLite database readiness is trivially healthy. **Options
convention for every new setting:** `AddOptions<T>().BindConfiguration("Section").ValidateOnStart()`
plus an `[OptionsValidator]` source-generated `IValidateOptions<T>` (DataAnnotations on the class);
`HealthEndpointsOptions` is the reference. The health-check package is pinned to EF Core's `10.0.12`.

**Impersonation** (`POST /api/v1/impersonation/tokens`, README "Impersonation"): a controlled
authentication bypass available in every environment. The command slice
(`Application/Features/Impersonation/IssueToken/`) is a non-transactional `ICommand` gated by the
`Impersonator` policy; its handler refuses chained impersonation, roles outside
`Impersonation:AssignableRoles` and (for non-administrators) roles the caller lacks, and the attempt is
recorded by the audit behavior (never the token). Application has no JWT dependency (architecture test):
it calls `IImpersonationTokenIssuer`, implemented by `JwtImpersonationTokenIssuer` in
`Api/Impersonation/`, signing with the separate `Impersonation:SigningKey` that
`ConfigureJwtBearerOptions` also trusts (only while `Impersonation:Enabled`). Tokens carry `act`
(RFC 8693 JSON object), `impersonated` and `imp_reason`; read them with `ImpersonationClaims`
(`IsImpersonated()`, `GetActorId()`). `ImpersonationOptions` (`Impersonation`) is validated on start,
including that the key differs from `Authentication:Jwt:SigningKey`; the disabled outcome is an
`Error` coded `IMPERSONATION_DISABLED` mapped to 404.

**Audit** (`Application/Common/Auditing/`, `Api/Audit/`): a separate append-only stream, never Serilog, never
sampled or level-filtered. `IAuditLog.RecordAsync(AuditEvent, ...)` is the seam (Application);
`FileAuditLog` (Api) writes JSON Lines to `audit-yyyyMMdd.jsonl` under `Audit:Directory` (`AuditOptions`,
default `logs/audit`, no off switch, never auto-deleted), lock-serialized, flushed per event, throwing on IO
failure. `AddApplication` registers `AuditBehavior` right after `LoggingBehavior` but no `IAuditLog`; the
host registers it (`AddAudit()`, after `AddApplication()`). `FailClosed` throws `AuditWriteFailedException`
(a 500; the token is not delivered); `BestEffort` logs Error (event ids 1100/1200) and proceeds, because
`TransactionBehavior` is inside `AuditBehavior` and the change has already committed. `ImpersonationAuditMiddleware`
(after `UseAuthentication`) records every request made under an impersonation token as `Impersonation.Request`
with the token's `jti`. `IAuditRequestContext` supplies trace id and source address (Api implements it over
`HttpContext`). Actor = the `act` subject when impersonated (`AuditIdentity`). Never put a token, secret,
`Authorization` header or body in an event. Api integration tests get a private temp audit directory per
`ProductsApiFactory` (`ReadAuditEvents()`); no test writes audit files under the repo.

**API versioning** (`Api/Http/ApiVersions.cs`, `Api/OpenApi/`; README "API versioning"): URL segment,
`Asp.Versioning.Mvc` + `.ApiExplorer`; both controllers are `[ApiVersion("1.0")]` on
`api/v{version:apiVersion}/...`, health/OpenAPI/Scalar are unversioned. A second `[Route]` per
controller (`ApiVersions.UnversionedAliasPrefix`, `Order = 1`) plus `AssumeDefaultVersionWhenUnspecified`
keeps `/api/products` and `/api/impersonation/tokens` working as a transitional alias for v1, not in
the OpenAPI document; `ReportApiVersions` puts `api-supported-versions` on every response.
`Location` and `Link` are always the canonical `/api/v1/...` URL (`ProductsController.VersionedUrl`).
A version that is not served is an unmatched route (404 problem, or 401 when anonymous). One OpenAPI
document per version via `AddVersionedOpenApi("v1")` (`Asp.Versioning.OpenApi` cannot be used: it needs
`Microsoft.OpenApi` 2.x, the built-in generator 3.x). Domain/Application/Infrastructure never reference
`Asp.Versioning` (architecture test). Integration tests take every URL from `tests/.../ApiRoutes.cs`.

**Rate limiting and proxies** (`Api/RateLimiting/`, `Api/Proxies/`): the built-in limiter with three
fixed-window policies, `Reads` / `Writes` / `Impersonation` (`RateLimitingOptions`, section `RateLimiting`,
read through `IOptions`: restart to change). Every controller action is limited: `MapControllers()
.WithDefaultRateLimiting()` gives an action that declares nothing `Reads`, `[EnableRateLimiting(...)]` picks
another, and only an explicit `DisableRateLimiting()` exempts (health, Development OpenAPI/Scalar); a test
walks every endpoint to enforce it. The partition (`RateLimitCaller`) is `user:<sub>`, the real `act` actor
for an impersonated token, else `ip:<address>`. Middleware order: forwarded headers (only with
`ForwardedHeaders:TrustedProxies`), trace id, request logging, exception handler, status-code pages, HTTPS
redirection, CORS, authentication, user log context, impersonation audit, request timeout, rate limiter,
authorization, endpoints. `RateLimitRejectionHandler` writes the `429` problem (`code` `RATE_LIMITED`, `Retry-After`) and,
for `Impersonation` only, a best-effort `RateLimited` audit event. The default test host uses maximal limits;
rate-limit tests use `factory.WithLimits(...)`. See README "Rate limiting".

**Request timeouts** (`Api/RequestTimeouts/`): the framework's `Microsoft.AspNetCore.Http.Timeouts`, options
`RequestTimeoutOptions` (section `RequestTimeouts`: `Default` 30 s, `Impersonation` 10 s; `TimeSpan`s, 1 ms to
10 min, read once). The default policy covers every endpoint that names none and does not opt out; only health
and the Development OpenAPI/Scalar say `DisableRequestTimeout()`; the token endpoint has `[RequestTimeout("Impersonation")]`.
`UseApiRequestTimeouts()` sits after the impersonation audit and before the rate limiter (so the audit records the
real 504). A timeout is `504` `application/problem+json`, `code` `REQUEST_TIMEOUT` (`RequestTimeoutResponseWriter`,
event 1400); a client abort is still swallowed silently. `TransactionBehavior` rolls back with
`CancellationToken.None` and logs a requested cancellation at Information. The middleware is a no-op under a
debugger. The default test host uses a 10 minute timeout; timeout tests use `factory.WithTimeouts(...)`. See README
"Request timeouts".

**OpenAPI contract check**: `OpenApiContractTests` compares the served `/openapi/v1.json` (normalized:
sorted keys, LF, no `servers`) with the committed `tests/MediatrUnionPoc.Api.IntegrationTests/Contracts/openapi.v1.json`.
A deliberate contract change regenerates it with `UPDATE_OPENAPI_SNAPSHOT=1 dotnet test ... --filter
"FullyQualifiedName~OpenApiContractTests"` (refused when `CI` is set) and commits the diff. See README "OpenAPI
contract check".

**Trace id and unhandled exceptions** (`Api/Http/`): `HttpContext.TraceId` (extension member; W3C
`Activity.Current` trace id, falling back to `HttpContext.TraceIdentifier`) is the one accessor. It
is stamped as the `traceId` member on every ProblemDetails body (`AddApiProblemDetails()` +
`UseStatusCodePages()`, so framework 400/404/415/500 too), as an `X-Trace-Id` header on every
response, and as a `TraceId` logging scope from `TraceIdMiddleware` (registered outermost, before
`UseExceptionHandler`, so the handler's log line is still inside the scope; Serilog surfaces the
scope as a `TraceId` property). `GlobalExceptionHandler` maps any unhandled exception to a 500
problem (exception text in `detail` only in Development), logs it once at Error, and swallows client
aborts. No exception-tracking service is bundled (OpenTelemetry and Sentry are the named options).

**CORS** (`Api/Cors/`): one default policy from `ApiCorsOptions` (`Cors` section, validated on start; `AddApiCors()` + `UseApiCors()`, no `[EnableCors]`). No config means no allowed origin and no CORS headers; `*` is rejected outright; only `appsettings.Development.json` lists (localhost) origins. Method, header and exposed-header lists are nullable so configured values replace the defaults (the binder appends to arrays); the exposed-headers defaults are the browser contract. `UseCors` sits after HTTPS redirection and before `UseAuthentication`, inside the trace-id middleware, so a preflight never meets the fallback policy and still carries `X-Trace-Id`. Test hosts run in Development: post-configure `ApiCorsOptions` to pin a list.

**Logging** (`Api/Logging/`): `ILogger` is the only logging API; Serilog is wired behind it in the Api
host only (`builder.Host.UseApiLogging()`, non-static, `preserveStaticLogger`), configured by the
`Serilog` section of `appsettings*.json` (console plus a rolling JSON file under `logs/`;
`Logging:LogLevel` is not used). Enrichers add application, version, environment, machine, process,
thread, `TraceId` and, after `UseAuthentication`, `UserId`/`IsImpersonated`/`ImpersonatedBy`
(`UseUserLogContext`). One request line per request (`UseApiRequestLogging`, outside the exception
handler, health probes at Debug, a handled 500 at Warning so the exception stays a single Error).
Hot-path messages are `[LoggerMessage]` methods with stable event ids (1000/1001 `LoggingBehavior`,
1100 `AuditBehavior`, 1200 audit middleware, 2000/2001 `GlobalExceptionHandler`). Never log tokens, `Authorization` headers or bodies. Api
integration tests read events from `ProductsApiFactory.LogSink` (a DI-registered `ILogEventSink`); the
factory silences the file and console sinks by configuration.

## Conventions specific to this repo

- Every async method this repo owns the signature of ends in `Async` and takes
  `CancellationToken cancellationToken = default`. The one exception is `Handle` on
  `IRequestHandler`/`IPipelineBehavior` implementations — MediatR's interfaces fix that method
  name and require a non-defaulted token, so those can't follow the convention.
- ASP.NET Core's `SuppressAsyncSuffixInActionNames` default (`true`) is overridden to `false` in
  `Program.cs` so controller action names keep the `Async` suffix — otherwise `nameof(GetByIdAsync)`
  used in `Url.Action` (the `Location` builder) silently stops matching the action name MVC would register it under.
- Central Package Management: every package version lives in `Directory.Packages.props`;
  individual `.csproj` files reference packages without a `Version` attribute.
- `.editorconfig` documents, with inline rationale, every analyzer severity override — check it
  before assuming a suppressed StyleCop/SonarAnalyzer rule is an oversight. Most notably: `SA1649`
  is disabled because it crashes outright on any file containing a `union` declaration (the
  analyzer predates the language feature).
- Every public/protected member has an XML doc comment (`GenerateDocumentationFile=true`, `CS1591`
  enabled repo-wide except the `CompileTimeChecks` scratch projects) — this is enforced, not
  aspirational. A new public member without a `<summary>` produces a build warning.
