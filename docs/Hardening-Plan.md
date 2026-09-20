# Hardening plan: .NET 11 features, real identity and operational concerns

Status: **in progress; steps marked "Status: implemented" are done.** This plan takes the POC from a stand-in identity
model to something a team could build on, using the platform features that arrived between .NET 8
and .NET 11. Each step is independently shippable and ends with the solution building and every test
passing.

## Decisions already made

| Topic | Decision |
| --- | --- |
| Identity | Real JWT bearer authentication; the header stand-ins (`X-Caller-Id`, `X-Admin`) are removed |
| Anonymous access | Only health checks and (Development only) the OpenAPI and Scalar documents. Every `GET` requires authentication, and so does the impersonation-token endpoint |
| Impersonation | Available to `Administrator` and `Support` roles, in **every** environment; a non-empty **reason** is mandatory and is recorded |
| Audit | A separate, simple audit stream (see step 5); a database table and Seq are deferred |
| Logging | Serilog with enrichment. Seq is **not** part of this plan (a new deployment is overkill for a POC) |
| Versioning | URL segment: `/api/v1/products`; the unversioned URLs stay as a transitional alias for v1 |
| CORS | A configurable policy for a future browser client; explicit origins only, none by default |
| Rate limiting | Built-in ASP.NET Core rate limiter, per-user partitioning |
| Language | No `field` keyword or primary-constructor work; only touch a file for other reasons |

## Step 1: Options plumbing and health checks

Status: implemented.

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

Status: implemented.

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

Status: implemented.

An endpoint, `POST /api/v1/impersonation/tokens`, that mints a short-lived JWT for a target identity.

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

**As built.** The command lives in `Application/Features/Impersonation/IssueToken/` (union
`ImpersonationToken | ValidationErrors | NotAuthorized | Error`, policy `Impersonator` = Administrator or
Support). Signing is behind `IImpersonationTokenIssuer`, implemented in Api, so Application references no JWT
library. The handler refuses chained impersonation, roles outside `Impersonation:AssignableRoles`, and (for a
non-administrator) roles the caller does not hold. Step 5 records every attempt, including those refused earlier in the
pipeline (the policy check, validation), through the audit stream. The README's
"Impersonation" section is the reference.

**Tests:** minting requires the role; reason is mandatory; token carries `act` and the marker; a
minted token is accepted by the API; expiry cap enforced; disabled switch returns `404`.

## Step 4: Serilog with enrichment

Status: implemented.

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

**As built.** The `TraceId` scope surfaces as a Serilog property without any change to
`TraceIdMiddleware` (confirmed by tests). The request line is written by Serilog's own logger, so it
takes `TraceId` and the user properties from the diagnostic context rather than the scope.
`UseSerilogRequestLogging` sits outside the exception handler and logs a handled 500 at `Warning`, so
`GlobalExceptionHandler` remains the only `Error`. The logger is non-static (`preserveStaticLogger`),
the `Logging:LogLevel` section is replaced by `Serilog`, and `[LoggerMessage]` works on the generic
`LoggingBehavior` directly. Tests capture events through an `ILogEventSink` registered in DI and
silence the file and console sinks by configuration. Only `Serilog.Formatting.Compact` was added to
the listed packages (explicit reference for the JSON formatters).

## Step 5: Audit stream

Status: implemented.

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

**As built.** One deliberate deviation from the plan text above: there is **no Serilog sub-logger**.
Serilog sinks swallow write failures, which defeats fail-closed auditing, so the stream is a small
dedicated writer instead. `IAuditLog.RecordAsync(AuditEvent, CancellationToken)` is in Application;
`FileAuditLog` in Api appends JSON Lines to `audit-yyyyMMdd.jsonl` (one file per UTC day, chosen by
the event's `TimeProvider` timestamp), under a lock, written through to disk, and throws on IO
failure. `AuditOptions` (`Audit:Directory`, default `logs/audit`) has no `Enabled` switch; the
application never deletes an audit file (retention is an operational decision). The events reach it
through a generic `AuditBehavior<TRequest,TResponse>`, registered right after `LoggingBehavior` so it
wraps authorization and validation and also records the attempts those refuse (the gap step 3
noted). Requests opt in with `IAuditableRequest<TResponse>`; the impersonation command and the four
product mutations do. A create learns its target by the request describing its own response
(`DescribeAudit(TResponse)` reads the id from the `ProductDto` case), so case types stay meaning-free.
`ImpersonationAuditMiddleware` (after authentication, before authorization) writes an
`Impersonation.Request` event for each request made under an impersonation token; the token's `jti` is
on both the mint event and every request event. Anonymous and failed-authentication requests are not
audited (no principal to attribute); they stay in the operational request log.

**Failure policy.** The impersonation mint is `FailClosed`: the failure is logged at Error and an
`AuditWriteFailedException` is thrown, which `GlobalExceptionHandler` answers with a `500`; the token
is never delivered. An unwritable audit store is an infrastructure fault, so it is an exception like a
database outage, not a union case. The product mutations and the request events are `BestEffort`:
`TransactionBehavior` sits inside `AuditBehavior`, so the change has already committed when the event is
written; the failure is logged at Error and the response proceeds. The README's "Audit stream" section
is the reference.

## Step 6: API versioning and CORS

Status: implemented.

**Versioning.**

Status: implemented.

`Asp.Versioning.Mvc` and `Asp.Versioning.Mvc.ApiExplorer`, URL-segment reader.
- Routes become `/api/v{version:apiVersion}/products` and `/api/v{version:apiVersion}/impersonation/tokens`;
  unversioned requests default to v1 (`AssumeDefaultVersionWhenUnspecified`) so existing callers keep
  working during the move. Every response reports `api-supported-versions` (`ReportApiVersions`).
- One OpenAPI document per version; existing transformers apply to each.
- `Location` and `Link` headers must emit the versioned URL. `CreatedAtAction` and the paging
  helpers need checking, and each gets a test.
- Move every integration test URL, the `.http` file and README examples.

**As built (versioning).** The unversioned URLs are kept as a transitional alias for v1 (a second
`[Route]` per controller with `Order = 1`, plus `AssumeDefaultVersionWhenUnspecified`); the alias is
not in the OpenAPI document, and the README's "API versioning" section says how to retire it.
`Location` and `Link` are always the canonical `/api/v1/...` URL, even for a client that used the
alias: `CreatedAtAction` is replaced by `Created(Url.Action(...))` so the versioned route is chosen
explicitly, and `SetPagingHeaders` takes an optional canonical URL. `Asp.Versioning.OpenApi` could
not be used (it needs `Microsoft.OpenApi` 2.x, `Microsoft.AspNetCore.OpenApi` 11 needs 3.x, NU1107),
so `AddVersionedOpenApi(name)` registers one document per version by hand, filtered by the API
explorer group, and its `AV0029`/`AV0030` advice is silenced in the Api project. With the version in
the path, a version that is not served is an unmatched route: a `404` problem (or `401` for an
anonymous caller, by the fallback policy), not a `400`. The Application layer has no versioning
dependency (architecture test).

**CORS.**

Status: implemented.

A named policy bound to `ApiCorsOptions` (allowed origins, methods, headers), empty by default.
Never a wildcard origin combined with credentials. Placed before authentication.
`WithExposedHeaders` lists the headers a browser must be able to read: `ETag`, `Link`,
`X-Total-Count`, `X-Trace-Id`, `Retry-After`. A pre-flight test asserts the policy.

**As built (CORS).** The wildcard origin is rejected outright rather than only when combined with
credentials: origins are an explicit list, validated on start as canonical `http`/`https` origins.
Localhost dev origins live in `appsettings.Development.json` only. The exposed list also carries
`Location` and `api-supported-versions`; `Retry-After` is listed ahead of the rate limiter that will
emit it (step 7). The method, header and exposed-header lists are nullable in the options class
because the configuration binder appends to arrays that hold defaults; the effective values come
from `GetAllowedMethods()` and its siblings. `UseCors` sits after HTTPS redirection and before
authentication, inside the trace-id middleware. ASP.NET Core answers a preflight with the full
allowed lists instead of refusing a disallowed method or header, and adds `Vary: Origin` only for
more than one configured origin; the tests assert those behaviours.

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

## Step 9: Dev container for agentic development

A reproducible environment on a Windows host in which Claude Code can build, test and navigate the
repo safely. Independent of the API work, so it can be done last.

**Windows-based means a Linux container on a Windows host.** Windows containers are the wrong tool:
the Dev Containers tooling, Claude Code's reference setup, the language servers and the code-graph
tools all target Linux, and Windows base images are very large. The container runs under Docker
Desktop with the WSL 2 backend and is opened from VS Code (Dev Containers) or the CLI. If a Windows
container is genuinely required it should be raised as a separate decision.

- **Isolated from the host OS.** The container must not be able to read or change the host beyond
  what is deliberately shared:
  - The repository is cloned into a **named Docker volume** (the Dev Containers "clone repository in
    container volume" flow), not bind-mounted from the Windows file system. This also avoids the slow
    Windows-to-WSL file bridge and CRLF surprises.
  - No other host mounts: no home directory, no `.ssh`, no `.aws`, no browser profiles. Git access is
    by a scoped, revocable token entered inside the container (or agent forwarding of a single
    deploy-scoped key), never by mounting host credentials.
  - No Docker socket and no `--privileged`; a non-root user with no sudo; dropped capabilities and
    `no-new-privileges`.
  - Host clipboard, GPU, USB and device pass-through off; ports forwarded only for the API's own
    port.
  - Network egress limited by the allow-list below, so an agent cannot exfiltrate to arbitrary hosts.
  - Claude's `~/.claude` lives on its own named volume and is separate from the host's.
  - Data leaves the container only through git (push to a branch) and explicit file export.
  The smoke test includes negative checks: the host's drive is not visible, the Docker socket is
  absent, and an off-list host is unreachable.
- `.devcontainer/devcontainer.json` + `Dockerfile` (or a base image plus Features): the exact SDK
  from `global.json` (.NET 11 preview, `allowPrerelease`), `git`, `gh`, **ripgrep** (the repo
  instructions require `rg` over `grep`), Node (Claude Code and Husky), and the repo's local dotnet
  tools restored (`dotnet tool restore`: csharpier, Husky.Net).
- **Language servers:** C# (Roslyn language server or `csharp-ls`, wired into Claude Code's LSP
  support so it gets symbol navigation and diagnostics), plus lightweight ones for Markdown, JSON,
  YAML and shell. The implementing agent verifies which C# server actually handles C# 15 `union`
  syntax on the preview SDK before committing to one, and reports honestly if none does yet.
- **Claude Code** installed in the image, with `~/.claude` on a named volume so login and memory
  survive rebuilds. No credentials are baked into the image or the repo.
- **Skills:** the `mattpocock-skills` plugin set installed and enabled by committed project settings,
  alongside the repo's own `csharp-union` skill if it is project-scoped. The agent verifies the
  current install mechanism rather than assuming one.
- **Code graph:** an MCP server that indexes the solution into a symbol and call graph so the agent
  can ask "who calls this" instead of grepping. Candidates to evaluate (verify each exists, is
  maintained, its licence, and that it handles C#): Serena (LSP-backed semantic tools) and a
  tree-sitter based graph server. Pick one, wire it in a committed `.mcp.json`, and document the
  reindex step.
- **Safety:** an egress allow-list (firewall init script in the style of Anthropic's reference dev
  container) so a container running with relaxed permission prompts can only reach NuGet, GitHub,
  the Anthropic API and the chosen docs sources.
- **Project settings:** a committed `.claude/settings.json` with a read-mostly permission allow-list
  (`dotnet build|test|format`, `rg`, read-only `git`), and a post-edit hook running
  `dotnet format whitespace` on changed files, so agent edits arrive already formatted.
- A `NuGet` cache volume for fast rebuilds, and a documented smoke test: open the container, run
  `dotnet build && dotnet test`, confirm the LSP answers a symbol query and the code graph answers a
  call query.

Suggested extras, each optional and separately switchable: Microsoft Learn and Context7 MCP servers
for current framework documentation, the GitHub MCP server for issues and PRs, git worktree support
so parallel agents do not share a working tree, and the same image reused by CI so local and CI
builds match.

**Tests/verification:** the container builds from scratch, `dotnet build` and `dotnet test` pass
inside it, and the smoke test above passes. Docs: a `docs/DevContainer.md` covering prerequisites
(Docker Desktop + WSL 2), first run, updating the SDK pin, and the safety model.

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
