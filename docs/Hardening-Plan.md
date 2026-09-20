# Hardening plan: .NET 11 features, real identity and operational concerns

Status: **proposed, nothing implemented yet.** This plan takes the POC from a stand-in identity
model to something a team could build on, using the platform features that arrived between .NET 8
and .NET 11. Each step is independently shippable and ends with the solution building and every test
passing.

## Decisions already made

| Topic | Decision |
| --- | --- |
| Identity | Real JWT bearer authentication; the header stand-ins (`X-Caller-Id`, `X-Admin`) are removed |
| Anonymous access | Only health checks, the impersonation-token endpoint's own credential flow, and (Development only) the OpenAPI and Scalar documents. Every `GET` requires authentication |
| Impersonation | Available to `Administrator` and `Support` roles, in **every** environment; a non-empty **reason** is mandatory and is recorded |
| Audit | A separate, simple audit stream (see step 5); a database table and Seq are deferred |
| Logging | Serilog with enrichment. Seq is **not** part of this plan (a new deployment is overkill for a POC) |
| Versioning | URL segment: `/api/v1/products` |
| CORS | A configurable stub policy for a future browser client |
| Rate limiting | Built-in ASP.NET Core rate limiter, per-user partitioning |
| Language | No `field` keyword or primary-constructor work; only touch a file for other reasons |

## Step 1: Options plumbing and health checks

**Options.** One convention for every new setting, extending the existing `HttpMappingOptions`:
`AddOptions<T>().BindConfiguration("Section").ValidateOnStart()`, validated by an `[OptionsValidator]`
source-generated `IValidateOptions<T>` (compile-time, no reflection). Registration lives in small
`IConfigureOptions<T>` classes or `Add...` extension methods so `Program.cs` stays a list of calls.
New options classes introduced by later steps: `JwtAuthOptions`, `ImpersonationOptions`,
`CorsOptions`, `RateLimitingOptions`, `RequestTimeoutOptions`. Use `IOptionsMonitor` only for values
where live reload is genuinely wanted (rate limits); `IOptions` otherwise.

**Health checks.** Framework-provided (`AddHealthChecks`, `MapHealthChecks`, no package for the
basics).
- `/health/live`: no checks; proves the process responds.
- `/health/ready`: a database check using `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`
  (`AddDbContextCheck`), selected by a `ready` tag.
- Both are `[AllowAnonymous]`-equivalent (mapped with `.AllowAnonymous()`), exempt from rate limiting,
  and return the default plain-text status (no detailed payload exposed publicly).
- Caveat to document: with the default private in-memory SQLite database the readiness check is
  trivially healthy; it is only meaningful with a real `ConnectionStrings:Products`.

**Tests:** integration tests for both endpoints (200 healthy; readiness `503` when the database check
is made to fail).

## Step 2: Real authentication, fallback policy and test identities

- Add `AddAuthentication().AddJwtBearer(...)` (options-bound) and `UseAuthentication()` before
  `UseAuthorization()`.
- **Fallback policy** `RequireAuthenticatedUser`: every endpoint, including `GET`, requires a caller
  unless explicitly `[AllowAnonymous]`. Secure by default.
- Controllers pass `User` into the commands; delete `ClaimsPrincipal.FromCallerHeaders` and the
  `X-Caller-Id` / `X-Admin` headers from the controller, the `.http` file, the OpenAPI document and
  the README.
- **Claim mapping decision:** set `MapInboundClaims = false` and read `sub` / `role`, or keep the
  default long-URI mapping. Either way the Application layer's `ClaimTypes.NameIdentifier` /
  `ClaimTypes.Role` usage and the JWT handler must agree; this is the most likely source of a bug, so
  it gets an explicit integration test.
- `IAuthorizationMiddlewareResultHandler` renders the framework's `401` and `403` as
  `application/problem+json` with the `traceId`, matching every other non-2xx response.
- **Tests:** a test authentication handler registered through `ProductsApiFactory` with a helper
  such as `client.AsUser("alice", roles: ...)`. Every existing integration test that sent the old
  headers moves to it. Add tests for `401` (no token), `403`, and anonymous access to health only.

## Step 3: Impersonation (user switching)

An endpoint (for example `POST /api/v1/dev/tokens`) that mints a short-lived JWT for a target identity.

- **Who:** callers with the `Administrator` or `Support` role. Nobody else, and never anonymously.
- **Request:** target user id, roles to grant, lifetime (capped by `ImpersonationOptions`), and a
  required **`reason`** (length-limited; optionally a ticket reference). Missing reason is a
  validation `400`, going through the same union pipeline as every other command.
- **Token contents:** the impersonated `sub` and roles, plus an `act` claim (RFC 8693) naming the
  real administrator, an `impersonated: true` marker claim, and the reason. Signed with a key
  separate from ordinary tokens.
- **Safeguards:** rate limited (step 7), configuration switch to turn it off per deployment,
  every attempt audited (success and failure), including denials.
- The endpoint is implemented as a normal MediatR command with a union result, so it follows the
  repo's own pattern and gets validation, authorization and logging for free.
- For manual testing without the endpoint, `dotnet user-jwts` can mint local tokens against the same
  validation settings.

**Tests:** minting requires the role; reason is mandatory; token carries `act` and the marker; a
minted token is accepted by the API; expiry cap enforced; disabled switch returns `404`.

## Step 4: Serilog with enrichment

Packages: `Serilog.AspNetCore` (includes the console sink), `Serilog.Settings.Configuration`,
`Serilog.Sinks.File`, `Serilog.Enrichers.Environment`, `Serilog.Enrichers.Process`,
`Serilog.Enrichers.Thread`.

- Configured from `appsettings` per environment (levels and sinks change without code).
- Sinks: console plus a rolling JSON file. **Seq is deliberately not included**; because Serilog
  sinks are configuration, adding one later needs no application change.
- Enrichers: log context, machine and environment name, process and thread id, application name and
  version, the authenticated user id, and `traceId`. Confirm the existing `TraceId` logging scope
  from `TraceIdMiddleware` surfaces as a Serilog property, not just as scope text.
- `UseSerilogRequestLogging()` replaces the framework's multi-line request logs with one structured
  line per request.
- Convert hot-path log calls (`LoggingBehavior`, `GlobalExceptionHandler`) to `[LoggerMessage]`
  source-generated methods with stable event ids. `ILogger` stays the only logging API in code.
- Never log tokens, `Authorization` headers or the impersonation reason's surrounding request body.

**Tests:** extend the existing `CapturingLoggerProvider` tests to assert the enriched properties;
unhandled-exception log still emitted once at Error with the trace id.

## Step 5: Audit stream

A separate, append-only record of security-relevant actions. Not diagnostic logging: it must not be
sampled, level-filtered or mixed into the operational log.

- `IAuditLog` abstraction in Application (`RecordAsync(AuditEvent, CancellationToken)`), so handlers
  and the impersonation command stay free of any logging or storage dependency.
- **Implementation for the POC:** a dedicated Serilog sub-logger writing JSON lines to its own rolling
  file (`audit-.jsonl`), selected by an `AuditStream` property so audit events can never appear in the
  operational sinks and vice versa. This is the "alternate stream": simple, no new infrastructure.
- Event shape: timestamp, actor (real identity, from `act` when impersonating), effective identity,
  action, target (type and id), outcome, **reason** (impersonation), `traceId`, source IP.
- Recorded events: every token minted or refused; every request made under an `impersonated` token;
  product mutations (create, update, patch, delete) with the actor.
- **Deferred:** an `AuditEvents` database table behind the same `IAuditLog` interface, for
  queryability and tamper-resistance. Swapping the implementation later changes no caller.

**Tests:** events are written for mint success and denial; reason is present; audit events do not
appear in the operational log capture; mutations record the actor.

## Step 6: API versioning and CORS

**Versioning.** `Asp.Versioning.Mvc` and `Asp.Versioning.Mvc.ApiExplorer`, URL-segment reader.
- Routes become `/api/v{version:apiVersion}/products`; unversioned requests default to v1
  (`AssumeDefaultVersionWhenUnspecified`) so existing callers keep working during the move.
- One OpenAPI document per version; existing transformers apply to each.
- `Location` and `Link` headers must emit the versioned URL. `CreatedAtAction` and the paging
  helpers need checking, and each gets a test.
- Move every integration test URL, the `.http` file and README examples.

**CORS.** A named policy bound to `CorsOptions` (allowed origins, methods, headers), empty or
localhost by default. Never a wildcard origin combined with credentials. Placed before
authentication. `WithExposedHeaders` lists the headers a browser must be able to read: `ETag`,
`Link`, `X-Total-Count`, `X-Trace-Id`, `Retry-After`. A pre-flight test asserts the policy.

## Step 7: Rate limiting

Built into the framework (`Microsoft.AspNetCore.RateLimiting`).
- Policies by intent: a generous `reads` policy and a stricter `writes` policy, plus a tight policy
  for the impersonation endpoint. Applied with `[EnableRateLimiting]`; health endpoints use
  `[DisableRateLimiting]`.
- Partition by authenticated user id, else remote IP. `UseRateLimiter` goes **after**
  `UseAuthentication`. Behind a proxy `UseForwardedHeaders` is required or all callers share one IP.
- `OnRejected`: `429` with `Retry-After`, rendered as ProblemDetails with the `traceId`; `429` added
  to the OpenAPI responses.
- Limits come from `RateLimitingOptions`.
- Documented limitation: limits are per instance and in memory; multi-instance enforcement needs a
  gateway or shared store.
- **Tests:** tiny limit with a long window, count requests, assert the `429` shape (no timing).

## Step 8: Request timeouts and the OpenAPI contract check

- `AddRequestTimeouts` with a default policy from `RequestTimeoutOptions`; a timed-out request maps to
  a ProblemDetails `504`. The `CancellationToken` already flows through every handler.
- OpenAPI generated at build time (`Microsoft.Extensions.ApiDescription.Server`); a test diffs it
  against a committed copy so an unintended contract change fails the build.

## Cross-cutting: documentation and tests

Each step updates the README sections it invalidates (the "Where the identity comes from" section
and the header table in "The HTTP contract" change in step 2; the routes in step 6), the API and
tests READMEs, and `CLAUDE.md`. `dotnet format whitespace` and the analyzers' documentation
requirement apply to all new public members.

## Deferred, with the reason

| Item | Why deferred |
| --- | --- |
| Seq | A new deployment is overkill for a POC. Serilog sinks are configuration, so adding it later is a config change |
| Audit database table | The audit stream is behind `IAuditLog`, so a table can replace the file implementation later |
| OpenTelemetry | Natural follow-up to Serilog and the trace id; not needed for the POC |
| Sentry or Application Insights | Exception grouping and alerting; a hosted-service decision |
| `HybridCache` | Nothing is cached yet |
| EF Core named query filters | No soft-delete or tenancy requirement |
| `Microsoft.Testing.Platform` | Worth a trial on the test projects, independent of everything above |
| EF Core 11 | `Directory.Packages.props` pins EF Core at `10.0.12` while the rest is `11.0.0-rc.1`; move when EF 11 ships |

## Open risks

- **Impersonation in every environment** is effectively a controlled authentication bypass. The role
  gate, mandatory reason, audit of every mint and the off switch are hard requirements, not extras.
- **Versioning moves every URL** and touches most integration tests; do it as its own commit.
- **Claim mapping** between the JWT handler and the Application layer (step 2) is easy to get subtly
  wrong; it has a dedicated test.
