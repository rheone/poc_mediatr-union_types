# Features

An outline of what this proof of concept does, the mechanism that enables each feature, where it
happens in the code, and why it is worth having. The [README](../README.md) has the full write-up;
this is the map. The last section lists features that are **planned but not yet built**, described
in [Hardening-Plan.md](Hardening-Plan.md).

The POC tests one idea: a C# 15 `union` as the response type of every MediatR command and query, so
each operation returns a closed, compiler-checked set of outcomes instead of throwing or returning
null.

## Implemented

### Core pattern

| Feature | What it does | Enabled by | Where | Why it helps |
| --- | --- | --- | --- | --- |
| **Union responses** | Every command and query returns a `union` of exactly the outcomes it can produce (for example `CreateProductResult`: product, validation errors, error, conflict) | C# 15 `union` declarations; MediatR `IRequest<TUnion>` | `Application/Features/Products/*/…Result.cs` | The set of outcomes is part of the type. Adding an outcome is a compile error at every place that has not handled it |
| **Exhaustive handling** | The controller `switch`es over the union and the compiler rejects a missing case (`CS8509`) | `switch` expressions over unions | `Api/Controllers/ProductsController.cs` | A forgotten outcome cannot reach production as a silent 500 or a wrong status |
| **No exceptions for expected outcomes** | Not-found, validation failure, conflict and so on are values, not throws | Union cases | All handlers | Control flow is visible in the signature, and exceptions stay reserved for real faults |
| **Meaning-free shared case types** | `Success`, `NotFound`, `Error`, `ValidationErrors`, `NotAuthorized`, `PreconditionFailed`, `Conflict` carry no built-in commit or rollback meaning | Small record types reused across unions | `Application/Common/Results/` | One vocabulary across the API; each union decides what a case means for it |
| **Compile-time proof of exhaustiveness** | Scratch projects prove a non-exhaustive `switch` genuinely fails the build | Tests shelling out to `dotnet build` | `tests/CompileTimeChecks/` | The safety claim is verified, not assumed |

### MediatR pipeline

| Feature | What it does | Enabled by | Where | Why it helps |
| --- | --- | --- | --- | --- |
| **Logging behaviour** | Logs each request and which union case came back | `IPipelineBehavior` | `Application/Common/Behaviors/LoggingBehavior.cs` | Uniform request tracing with no per-handler code |
| **Authorization behaviour** | Checks the caller against a named policy before anything else | `IRequiresAuthorization` + `IAuthorizable<TSelf>` | `AuthorizationBehavior.cs`, `Common/Authorization/` | Who is calling is decided before their input is even validated |
| **Validation behaviour** | Runs FluentValidation and short-circuits to the union's `ValidationErrors` case | `IValidatable<TSelf>` (static abstract `FromValidationErrors`) | `ValidationBehavior.cs`, `*Validator.cs` | Generic code can build a union it has never seen; handlers only see valid input |
| **Transaction behaviour** | Wraps commands in a unit of work, committing or rolling back according to the outcome | `ITransactionalCommand`, `ITransactionOutcome<T>.ShouldCommit` | `TransactionBehavior.cs` | Rollback rules live with each union as an exhaustive `switch`, not in a hard-coded list |
| **Commit failures classified per operation** | A refused commit (`ConcurrencyConflict`, `UniqueViolation`) becomes that operation's own union case | `ICommitFailable<TSelf>.FromCommitFailure` | Each `…Result.cs` | The compiler forces every transactional operation to decide what a database refusal means |
| **Fixed pipeline order** | Logging, then authorization, then validation, then transaction | Registration order | `Application/DependencyInjection.cs` | Cheap checks first; nothing runs a transaction for a request that was going to be rejected |

### Domain and data

| Feature | What it does | Enabled by | Where | Why it helps |
| --- | --- | --- | --- | --- |
| **Vertical slices** | Each operation's command, handler, result and validator live together | Folder-per-operation layout | `Application/Features/Products/<Operation>/`, `Application/Features/Impersonation/IssueToken/` | A change to one operation touches one folder |
| **Value objects** | `ProductId`, `Money`, `ProductVersion` are typed, validated values, not primitives | Vogen | `Domain/` | Invalid values cannot exist and arguments cannot be swapped by accident |
| **Layered dependencies** | Domain depends on nothing; Application on Domain; Infrastructure and Api on both | Project references, `NetArchTest` rules | `tests/MediatrUnionPoc.ArchitectureTests/` | The layering is enforced by a failing test, not by convention |
| **Repository and unit of work** | The domain defines the interfaces, EF Core implements them; commit returns a `CommitResult` union | `IProductRepository`, `IUnitOfWork` | `Domain/`, `Infrastructure/` | The domain stays free of EF Core; a failed commit is a value |
| **Real relational persistence** | SQLite everywhere (private in-memory by default, a file when configured), no EF InMemory provider | EF Core SQLite | `Infrastructure/`, `ConnectionStrings:Products` | Constraints, transactions and concurrency behave as in production |
| **Unique product names** | Names are unique ignoring case and surrounding whitespace, checked up front and backed by a unique index | `ProductNames.Normalize`, `ExistsWithNameAsync`, unique index reported as `UniqueViolation` | `Domain/ProductNames.cs`, `ProductRuleExtensions` | Friendly `409` in the common case, and the index catches the race |
| **Optimistic concurrency** | Every mutation advances a version, exposed as a weak `ETag`; `PUT`/`PATCH` require `If-Match` (428/400/412) | `ProductVersion`, `IfMatchHeader.Parse` | `Domain/ProductVersion.cs`, `Api/Http/IfMatchHeader.cs` | Lost updates are impossible without holding locks |
| **Listing, filtering, sorting, paging** | `GET /api/products` with an allow-listed sort vocabulary, deterministic ordering and paging metadata | `ProductCriteria`, `ProductSort`, `PagedResult<T>`, `X-Total-Count` and RFC 8288 `Link` headers | `Domain/`, `ProductRepository`, `Api/Http/PagingHttpExtensions.cs` | Callers cannot sort by arbitrary columns; pages are stable |
| **Injectable clock** | `CreatedAt` is stamped from a `TimeProvider` and stored as UTC ticks so SQLite can order it | `TimeProvider`, `UtcTicksValueConverter` | `CreateProductHandler`, `Infrastructure/ValueConverters.cs` | Deterministic time in tests and correct ordering in SQLite |

### HTTP surface

| Feature | What it does | Enabled by | Where | Why it helps |
| --- | --- | --- | --- | --- |
| **Union to HTTP mapping** | Each union arm becomes an RFC 7807 `application/problem+json` response in one line | C# 14 extension members with per-call overrides | `Api/Http/ResultHttpExtensions.cs` | Controllers stay a plain `switch` and the status policy is in one place |
| **Configurable status policy** | `Error.Code` to status table and `type` URI rules | DI-registered `HttpMappingOptions` (`AddResultHttpMapping`) | `Api/Http/HttpMappingOptions.cs` | Policy changes without touching controllers |
| **JSON Merge Patch** | `PATCH` updates only supplied members, distinguishing absent from null | `Optional<T>`, `OptionalJsonConverterFactory`, `[Consumes(application/merge-patch+json)]` | `Application/Common/Optional.cs`, `Api/Http/` | Partial updates without hand-written null-versus-missing logic |
| **Trace id everywhere** | One id on every problem body, an `X-Trace-Id` header on every response, and a logging scope | `TraceIdMiddleware`, `HttpContext.TraceId` | `Api/Http/TraceIdMiddleware.cs`, `HttpContextTraceExtensions.cs` | A client-reported id finds the matching log lines and trace |
| **Global exception handling** | Unexpected exceptions become a `500` problem body; details shown only in Development; client aborts are swallowed | `IExceptionHandler` | `Api/Http/GlobalExceptionHandler.cs` | No stack traces leak, no noisy logs for cancelled requests |
| **Health checks** | Anonymous `/health/live` (no checks) and `/health/ready` (database round trip), plain-text status only | `AddHealthChecks`, `AddDbContextCheck` tagged `ready`, `MapHealthChecks(...).AllowAnonymous()` | `Api/Health/` | Orchestrators and load balancers can tell alive from ready |
| **Structured, enriched logging** | One structured line per request plus enriched events (trace id, user id, impersonation flag and actor, application, version, environment, machine, process, thread) to the console and a rolling daily JSON file; never tokens, `Authorization` headers or bodies. Seq is not included | Serilog behind `ILogger` (`UseSerilog`, `UseSerilogRequestLogging`), configured by the `Serilog` section of `appsettings*.json`; `[LoggerMessage]` methods with stable event ids | `Api/Logging/`, `Application/Common/Behaviors/LoggingBehavior.cs`, `Api/Http/GlobalExceptionHandler.cs` | Logs are searchable by `traceId` and user, sinks and levels change by configuration, and an unhandled exception is one Error entry |
| **Validated options convention** | Settings bind from configuration and are validated when the host starts | `AddOptions<T>().BindConfiguration().ValidateOnStart()` with an `[OptionsValidator]` source-generated validator (plus a hand-written `IValidateOptions` for cross-field rules where needed) | `Api/Health/HealthEndpointsOptions.cs`, `Api/Impersonation/ImpersonationOptions.cs` | A bad setting stops startup instead of failing at runtime; one pattern for every future setting |
| **OpenAPI and Scalar UI** | Generated OpenAPI with `ETag`, paging and precondition headers and example bodies; Scalar reference UI in Development | `Microsoft.AspNetCore.OpenApi` transformers, Scalar | `Api/OpenApi/` | The documented contract includes the conditional-request behaviour |

### Authorization

| Feature | What it does | Enabled by | Where | Why it helps |
| --- | --- | --- | --- | --- |
| **Role-based** | `DELETE` requires the `Administrator` role, checked in the pipeline before validation | `IRequiresAuthorization`, `AdministratorAuthorizationHandler` | `Application/Common/Authorization/`, `AuthorizationBehavior` | Reusable gate: any command opts in by implementing one interface |
| **Resource-based** | `PUT`/`PATCH` require the caller to own the product, checked in the handler once it is loaded | `ResourceAuthorizationService`, `OwnerAuthorizationHandler<TResource>` | `Features/Products/Common/ProductChangeExtensions.cs` | Ownership needs the loaded entity, so it cannot run earlier |
| **Real authentication** | JWT bearer tokens identify the caller: `sub` becomes the product owner, a `role` of `Administrator` allows `DELETE`. Every endpoint requires a caller (fallback policy) except health checks and, in Development, the OpenAPI and Scalar documents; the middleware's `401`/`403` are problem bodies with a `traceId` | `AddJwtAuthentication`, `JwtAuthOptions`, `RequireAuthenticatedUser` fallback policy, `ProblemDetailsAuthorizationResultHandler` | `Api/Authentication/` | Secure by default, and the Application layer still only sees a `ClaimsPrincipal`. A host with no signing key outside Development refuses to start |
| **Impersonation** | `POST /api/impersonation/tokens` lets an `Administrator` or `Support` caller mint a short-lived token acting as another identity, in every environment. A reason (10 to 500 characters) is mandatory and recorded; no chained impersonation; only assignable roles, and a non-administrator may grant only roles they hold; the lifetime is capped; a configuration switch turns it off (404) | A normal command slice with the `Impersonator` policy (`AdministratorRequirement("Administrator", "Support")`), `IImpersonationTokenIssuer` implemented in Api, a separate signing key accepted by the bearer scheme alongside the ordinary one, `ImpersonationOptions` (validated on start), RFC 8693 `act` plus `impersonated` and `imp_reason` claims | `Application/Features/Impersonation/IssueToken/`, `Application/Common/Authorization/ImpersonationClaims.cs`, `Api/Impersonation/`, `Api/Controllers/ImpersonationController.cs` | Support can reproduce what a user sees, accountably, without their credentials. It is a controlled authentication bypass, so the role gate, mandatory reason, separate key, no chaining, lifetime cap and off switch are hard requirements. Each attempt that reaches the handler is logged once, never with the token |

### Quality and tooling

| Feature | What it does | Enabled by | Where | Why it helps |
| --- | --- | --- | --- | --- |
| **Five test projects** | Domain unit tests, Application tests (mocked repository), real-SQLite infrastructure tests, full-HTTP integration tests, architecture tests | xUnit v3, NSubstitute, `WebApplicationFactory`, NetArchTest | `tests/` | Each layer is tested at the level that gives real confidence |
| **Analyzers enforced** | StyleCop, Sonar and threading analyzers; a public member without XML docs warns | `.editorconfig`, `GenerateDocumentationFile` | Repo-wide | Consistency and documentation are not optional |
| **Central package management** | Every package version in one file | `Directory.Packages.props` | Repo root | One place to upgrade |
| **Pinned preview SDK** | The exact .NET 11 preview SDK is fixed | `global.json` | Repo root | Every machine and CI job builds with the same compiler |

## Planned (not yet built)

See [Hardening-Plan.md](Hardening-Plan.md) for scope, order and tests.

| Feature | Intended mechanism | Why |
| --- | --- | --- |
| Separate audit stream | `IAuditLog` writing to its own Serilog JSON file | Who did what, and why, kept apart from diagnostic logs |
| URL-segment API versioning | `Asp.Versioning.Mvc` | `/api/v1/…` lets the contract evolve without breaking clients |
| CORS stub | Named policy from options, exposing the headers a browser needs | Ready for a browser client |
| Rate limiting | Built-in rate limiter, per-user partitions | Protects the API and the impersonation endpoint |
| Request timeouts and OpenAPI contract check | `AddRequestTimeouts`, build-time OpenAPI diff test | Bounded requests and no accidental contract drift |
| Dev container for agentic development | Linux container on a Windows host with the pinned SDK, language servers, Claude Code, skills and a code-graph MCP server | One command to a safe, reproducible environment in which an agent can build, test and navigate the code |
