# MediatrUnionPoc.Api.IntegrationTests

Integration tests for `MediatrUnionPoc.Api` — the one place in the suite that boots the real
ASP.NET Core host end-to-end: real DI container, real MediatR pipeline, real controller routing,
and a real (SQLite in-memory) database, exercised through actual HTTP requests via
`Microsoft.AspNetCore.Mvc.Testing`'s `WebApplicationFactory`.

- `ProductsApiFactory.cs` — boots the real host with nothing substituted: with no
  `ConnectionStrings:Products` value each host gets its own private in-memory SQLite database (its
  schema created at startup), so tests using their own factory never see another test's data.
- `ApiRoutes.cs` — the one place the tests spell a URL: the versioned routes (`/api/v1/products`,
  `/api/v1/impersonation/tokens`, `/openapi/v1.json`) every ordinary test uses, plus the `Unversioned`
  members only the alias tests use.
- `ApiVersioningTests.cs` — URL-segment versioning: the transitional unversioned alias behaves as the
  versioned route for each verb group (create, get, list, put, patch, delete, impersonation),
  `api-supported-versions` on every versioned response (and not on health or OpenAPI), `Location` and
  `Link` always on the versioned URL (also when the alias or `v1.0` was used), a version that is not
  served is a `404` problem with the trace id (`401` when anonymous), and the v1 OpenAPI document lists
  only the versioned paths, each operation once, with Scalar pointing at it.
- `CorsTests.cs` — the CORS policy over real HTTP with the fallback policy in place: the allowed origin
  is echoed exactly (with `Vary: Origin` for several origins), a refused origin or an empty
  configuration gets no `Access-Control-*` header, a `PATCH` preflight with `If-Match` and
  `application/merge-patch+json` is answered with no credentials (while the same anonymous caller gets
  `401` on a real request) and carries `X-Trace-Id`, disallowed methods and headers are not listed, the
  exposed-headers list appears on real responses and each header the API emits is present, credentials
  are off unless configured, an authenticated cross-origin write works, and configured lists replace
  the defaults. The host runs in Development, so tests that need a specific list post-configure
  `ApiCorsOptions`.
- `CorsOptionsTests.cs` — the CORS defaults, every validation rule (origins, wildcard, duplicates,
  method and header tokens, max-age bounds, each also refusing host start) and a check that
  [`docs/operations.md`](../../docs/operations.md) names every default method, header and exposed header.
- `RateLimitingTests.cs` — the limiter over real HTTP with tiny limits and hour-long windows (count
  requests, never wait): the exact `429` problem (`type`, `title`, `status`, `code` `RATE_LIMITED`, `traceId`
  equal to `X-Trace-Id`, whole-second `Retry-After`), the `Warning` log with policy and partition kind but no
  address, reads and writes on independent budgets, two users and two addresses on independent budgets, an
  address budget that can never be an authenticated caller's (even a user named like the address), a `401` and
  a `403` still spending budget, health and OpenAPI never limited, a CORS preflight never counted or refused,
  and `Retry-After` exposed on a cross-origin `429`.
- `RateLimitingImpersonationTests.cs` — with real JWTs: impersonated requests spend the real actor's budget
  (and are audited as `429`), minting is limited by its own tighter budget, a refused mint is audited
  (`RateLimited`, actor, source address, trace id; no actor when anonymous), a refusal by another policy is
  not, an audit write failure is logged at `Error` and the `429` still goes out, and a bad token still spends
  the address budget.
- `RateLimitingSecureByDefaultTests.cs` — a throwaway controller (`TestData/RateLimitProbeController`, added as
  an application part) proves an unannotated action is limited, a declared policy beats the default and
  `DisableRateLimiting` exempts; walks every mapped endpoint to prove each declares a policy or is an
  explicit operational exemption, and that each controller action's policy fits its verb.
- `RateLimitingOptionsTests.cs` — defaults (equal in code, `appsettings.json` and the [operations page](../../docs/operations.md) table), every
  bound of every policy, host start refusing an invalid limit, and a configuration reload (valid or invalid)
  changing nothing until restart. `RateLimitingOpenApiTests.cs` — every operation declares the `429` with its
  header and example.
- `RequestTimeoutTests.cs` — the timeout over real HTTP with tens of milliseconds and a substituted sender that
  waits on its token (no wall-clock waits): the exact `504` problem (`type`, `title`, `status`, `code`
  `REQUEST_TIMEOUT`, `traceId` equal to `X-Trace-Id`), one Warning and no Error (request line included), a named
  policy beating the default, an unannotated action timed out and a `[DisableRequestTimeout]` one not, health never
  timed out, a client abort still swallowed silently (no `504`), a transactional `POST` cancelled inside its
  transaction creating nothing without an Error, and the fail-closed impersonation mint: cancelled by the deadline
  it delivers no token and is audited as `Exception`, while a merely slow audit write outlives the deadline and the
  token is delivered only after its event. They assume no debugger is attached. `TestData/RequestTimeoutProbeController`
  and `RequestTimeoutTestSupport` (`WithTimeouts`, `WithTimeoutProbe`) are the arrangement; the default host uses a
  10 minute timeout.
- `RequestTimeoutSecureByDefaultTests.cs` — walks every mapped endpoint: no controller action is exempt, only the
  token endpoint names a policy, every exempt endpoint is operational, and the framework holds a default and a named
  policy. `RequestTimeoutOptionsTests.cs` — defaults (equal in code, `appsettings.json` and the [operations page](../../docs/operations.md) table), every
  bound, host start refusing an invalid value. `RequestTimeoutOpenApiTests.cs` — every operation declares the `504`.
- `OpenApiContractTests.cs` (with `TestData/OpenApiContract.cs` and `Contracts/openapi.v1.json`) — the OpenAPI
  contract check: the live `v1` document, normalized, equals the committed snapshot; versioned paths only; no signing
  key, token or local path in it; the snapshot is canonical (LF, sorted, one trailing newline) and stable across
  fetches. `UPDATE_OPENAPI_SNAPSHOT=1` rewrites it (never with `CI` set); see the
  [OpenAPI contract check](../../docs/operations.md#openapi-contract-check-no-accidental-drift). `OpenApiContractComparerTests.cs` — against a mutated copy of the snapshot an added path, a removed response
  code and a changed schema property are each detected, the failure message names operations, caps the list and gives
  the regeneration command, and key order, line endings and `servers` are not differences.
- `ForwardedHeadersTests.cs` — with no trusted proxy a spoofed `X-Forwarded-For` cannot change the rate-limit
  partition; with trusted proxies (an address or a CIDR network) the forwarded client is the partition and the
  audit `sourceIp`, and from an untrusted sender the header is ignored; entry validation and host start.
- `ApiAuthentication.cs`, `TestAuthenticationHandler.cs`, `TestIdentityExtensions.cs` — how tests state
  who is calling. By default the factory registers a header-driven test scheme as the default
  authentication scheme, and `client.AsUser("alice", roles)` (or `request.AsUser(...)` for one request)
  sets the identity; a plain client is anonymous. `new ProductsApiFactory(ApiAuthentication.RealJwt)`
  leaves the production bearer scheme as the default instead. The fallback policy and the rest of the
  pipeline are the real ones in both modes.
- `AuthenticationTests.cs` — `401` on every verb with no credentials (problem body, `traceId`), the
  middleware's `403` shape, anonymous health and OpenAPI, the `Bearer` scheme in the OpenAPI document,
  the one coherent policy set, and options validation at start (no key outside Development).
- `JwtBearerAuthenticationTests.cs` (with `TestData/JwtTestTokens.cs`) — real signed tokens: expired,
  wrongly signed, wrongly addressed and malformed ones are `401`; `sub` becomes the owner and a `role`
  claim of `Administrator` passes `DELETE`; a token with no `sub` cannot create.
- `ImpersonationEndpointTests.cs`, `ImpersonationTokenTests.cs`, `ImpersonationOptionsTests.cs` (with
  `TestData/ImpersonationTestSupport.cs`), all on the real JWT scheme — `POST /api/v1/impersonation/tokens`:
  `401` anonymous, `403` for a plain user, Support can mint a plain or `Support` token but not an
  `Administrator` one, an administrator can, mandatory reason and lifetime cap as per-field `400`s, the
  `404` off switch (and `401` still for anonymous), `Cache-Control: no-store`, no token or audit content in any log, and the OpenAPI declaration. The token tests validate a minted token with the
  host's own bearer handler and read `act`, the marker and the reason back on the principal, use it
  against protected endpoints (it owns what it creates, its roles apply), check the two accepted signing
  keys against a random one, expiry (crafted and minted on a past clock), chained impersonation, and that
  switching impersonation off stops the impersonation key being trusted. The options tests cover
  start-up validation (key required outside Development while enabled, not the ordinary key, lifetimes).
- `AuditStreamTests.cs`, `FileAuditLogTests.cs`, `AuditOptionsTests.cs` — the audit stream over real HTTP
  with real JWTs: a mint is audited (actor, effective id, roles, reason, ticket, `tokenId` equal to the
  token's `jti`, trace id, source address), refusals by the handler, by the `Impersonator` policy and by
  validation are audited, every request under a minted token is an `Impersonation.Request` event with the
  same `tokenId` (ordinary reads and health probes are not), the four product mutations carry actor and
  target, audit and operational content never mix, an unwritable audit directory fails a mint closed (no
  token anywhere in the response) and lets a create through with an Error logged; the writer against a
  temp directory (append, daily roll, concurrent writers, a forged line, IO failure); and the options.
- `ProductsControllerTests.cs` — exercises the union-to-HTTP-status mapping each controller
  action's `switch` performs, end to end through routing and the real MediatR pipeline behaviors.
  A fresh `ProductsApiFactory` per test gives each test its own isolated database.

- `ProductListingTests.cs` — the list endpoint's query-string contract over HTTP: filter and sort
  binding, per-field `400`s, the paging metadata in the body, `X-Total-Count`, and the `Link`
  header's exact URLs. It swaps in a manually controlled `TimeProvider` (`TestData/ManualTimeProvider`)
  so creation timestamps are deterministic.
- `PatchProductTests.cs` — `PATCH` (JSON Merge Patch): absent versus present members, `415` for other
  media types, `If-Match` rules, ownership, duplicate names and racing patches.
- `DuplicateProductNameTests.cs` — the `409` duplicate-name rule over HTTP, including racing `POST`s
  settled by the unique index.
- `ResultHttpMappingTests.cs` — the `ToProblemResult` extension members and `HttpMappingOptions`
  (custom error-code statuses, per-call overrides, RFC 7807 shape).
- `TraceIdTests.cs`, `TraceIdMiddlewareTests.cs` — the trace id in problem bodies, the `X-Trace-Id`
  header, and the log scope (surfacing as a `TraceId` property on real Serilog events).
- `LoggingTests.cs`, `RequestLogPropertiesTests.cs` — the Serilog setup: enriched properties on an
  ordinary `ILogger` event (trace id, user, application, environment, machine, process, thread, event
  id), `IsImpersonated`/`ImpersonatedBy` under a minted impersonation token, the request line
  (status, elapsed, trace id, user), health probes silent at the default level and `Debug` when it is
  lowered, a configuration level override honoured, an invalid level failing host start, and no
  bearer token, impersonation token or `Authorization` header in any captured event.
- `GlobalExceptionHandlerTests.cs`, `UnhandledExceptionTests.cs` — the `500` problem for an
  unexpected exception, Development-only `detail`, exactly one `Error` log entry carrying the trace
  id (the request line for the 500 is a `Warning`), and client aborts (using `ThrowingSender` and
  `BlockingSender`).

**Audit files.** Every `ProductsApiFactory` host writes its audit stream to its own directory under the
system temp path (`factory.AuditDirectory`, set through `Audit:Directory`), never under the repository;
`factory.ReadAuditEvents()` reads the events back, and the directory is removed on dispose (and at process
exit as a backstop). The factory also gives every request a fixed remote address, since the in-memory
server has none (a test overrides it per request with the `X-Test-Remote-Address` header, and
`TestData/RateLimitTestSupport` does that for the rate-limiting tests). A test makes the audit path unwritable by pointing `Audit:Directory` at an existing file.

**Rate limits.** The factory sets every policy's limit to the maximum the options allow, so no ordinary test
ever meets the limiter; a rate-limiting test derives a host with `factory.WithLimits(reads: 2, ...)`, which
sets a tiny limit and an hour-long window through configuration.

**Capturing logs.** `ProductsApiFactory` runs the real Serilog setup and registers a
`CapturingLogEventSink` (`factory.LogSink.Events`, shared with derived hosts), which sees every
enriched property. It also restricts the file and console sinks to `Fatal` by configuration, so no
`logs/` file is written (the rolling file sink opens its file on the first write) and test output
stays quiet. Override levels per test with `UseSetting("Serilog:MinimumLevel:...", ...)`.
`CapturingLoggerProvider` remains for the two tests that build a bare `LoggerFactory` without a host.

- `HealthEndpointTests.cs` — `/health/live` and `/health/ready`: `200` plain-text `Healthy`,
  readiness `503` when a `DbConnectionInterceptor` makes the database connection fail, liveness
  unaffected, no credentials needed, no JSON check details, configurable paths, and a
  malformed path failing options validation at host start.
- `OptionalJsonConverterTests.cs` — `Optional<T>` binding.
- `ListingOpenApiTests.cs`, `ConcurrencyOpenApiTests.cs`, `PatchOpenApiTests.cs`,
  `ProductContractExampleTransformerTests.cs` — what the generated OpenAPI document declares:
  list parameters and paging headers, the `ETag` header and `409`/`412`/`428` responses, the merge
  patch media type, and the example bodies.

Nothing here substitutes any dependency other than the database name — this is deliberately the
one seam in the suite that crosses every layer at once. Tests that need a real database but not a
real HTTP host live in `MediatrUnionPoc.Infrastructure.IntegrationTests` instead; tests that need
neither live in `MediatrUnionPoc.Application.Tests`.

## Dependencies

**Project references:** all four `src/` projects — `MediatrUnionPoc.Api`,
`MediatrUnionPoc.Application`, `MediatrUnionPoc.Domain`, `MediatrUnionPoc.Infrastructure` — since
booting the real host pulls in the fully composed application.

**Key packages:**

- `xunit.v3` / `xunit.runner.visualstudio` — test framework (xUnit v3; the test project is an executable) and VSTest runner.
- `Microsoft.AspNetCore.Mvc.Testing` — in-memory `TestServer` and `WebApplicationFactory<Program>`.
- `coverlet.collector` / `Microsoft.NET.Test.Sdk` — coverage collection and the `dotnet test` host.

Also carries the repo-wide analyzer package set (`AsyncFixer`, `IDisposableAnalyzers`,
`Microsoft.VisualStudio.Threading.Analyzers`, `SonarAnalyzer.CSharp`, `StyleCop.Analyzers`), and a
global `Using Include="Xunit"` so test files don't each need `using Xunit;`.

```mermaid
flowchart LR
    Domain[MediatrUnionPoc.Domain]
    Application[MediatrUnionPoc.Application]
    Infrastructure[MediatrUnionPoc.Infrastructure]
    Api[MediatrUnionPoc.Api]
    ApiIT[MediatrUnionPoc.Api.IntegrationTests]:::here

    Application --> Domain
    Infrastructure --> Domain
    Infrastructure --> Application
    Api --> Domain
    Api --> Application
    Api --> Infrastructure
    ApiIT --> Api
    ApiIT --> Application
    ApiIT --> Domain
    ApiIT --> Infrastructure

    classDef here fill:#ffefc2,stroke:#c98a00,stroke-width:2px;
```

## Usage

```bash
dotnet test tests/MediatrUnionPoc.Api.IntegrationTests
```

Slower than the unit-test projects (each test boots a real host), but still fast in absolute terms
since the database is an in-memory SQLite one, not a real server round-trip.
