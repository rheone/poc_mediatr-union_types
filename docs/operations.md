# Health checks and options

Part of the [documentation](index.md).

**Contents**

- [Health checks and options](#health-checks-and-options)
- [CORS: letting a browser client call the API](#cors-letting-a-browser-client-call-the-api)
  - [Options](#options)
  - [No wildcard origin](#no-wildcard-origin)
  - [The exposed-headers contract](#the-exposed-headers-contract)
  - [Where it sits in the pipeline, and why](#where-it-sits-in-the-pipeline-and-why)
  - [Allowing a new origin](#allowing-a-new-origin)
  - [Warning: `AllowCredentials`](#warning-allowcredentials)
- [Rate limiting: a budget per caller](#rate-limiting-a-budget-per-caller)
  - [Options](#options-1)
  - [Who is counted](#who-is-counted)
  - [Every endpoint is limited unless it says otherwise](#every-endpoint-is-limited-unless-it-says-otherwise)
  - [Where it sits in the pipeline](#where-it-sits-in-the-pipeline)
  - [The `429` response](#the-429-response)
  - [Behind a reverse proxy](#behind-a-reverse-proxy)
  - [Limitations](#limitations)
- [Request timeouts: a deadline per request](#request-timeouts-a-deadline-per-request)
  - [Options](#options-2)
  - [Every endpoint is covered unless it says otherwise](#every-endpoint-is-covered-unless-it-says-otherwise)
  - [Where it sits in the pipeline](#where-it-sits-in-the-pipeline-1)
  - [The `504` response](#the-504-response)
  - [Cancellation: what is and is not interrupted](#cancellation-what-is-and-is-not-interrupted)
  - [Debugger caveat](#debugger-caveat)
- [OpenAPI contract check: no accidental drift](#openapi-contract-check-no-accidental-drift)
  - [How it works](#how-it-works)
  - [When it fails](#when-it-fails)
  - [Updating the snapshot deliberately](#updating-the-snapshot-deliberately)

Two anonymous probe endpoints, mapped with `.AllowAnonymous().DisableRateLimiting().DisableRequestTimeout()` (a probe must never be
refused or given a deadline of its own) and answering the framework's default
plain-text status only (`Healthy`, `Degraded` or `Unhealthy`; never a JSON body of check details):

| Endpoint | Runs | Answers |
| --- | --- | --- |
| `GET /health/live` | no checks; proves the process responds | `200` |
| `GET /health/ready` | the checks tagged `ready`: a database round trip through `AppDbContext` (`AddDbContextCheck`, `SELECT 1`) | `200`, or `503` when the database cannot be reached |

The paths come from the `HealthEndpoints` configuration section (`LivePath`, `ReadyPath`; both must
start with `/`). Caveat: with the default private in-memory SQLite database the readiness check is
trivially healthy; it only says something with a real `ConnectionStrings:Products`.

**The options convention.** Every settings class is registered as
`AddOptions<T>().BindConfiguration("Section").ValidateOnStart()` and validated by an
`[OptionsValidator]` source-generated `IValidateOptions<T>` built from DataAnnotations on the class
(compile-time, no reflection), so a bad value stops the host at start. `HealthEndpointsOptions`
(`Api/Health/`) is the reference implementation; `JwtAuthOptions` (`Authentication:Jwt`) and
`ImpersonationOptions` (`Impersonation`) follow it, the latter adding a second, hand-written
`IValidateOptions` (`ImpersonationOptionsRules`) for rules that span members or another options class
(key required while enabled and different from the ordinary key, default lifetime not above the
maximum). `HttpMappingOptions` predates the convention and is configured in code only.

# CORS: letting a browser client call the API

A browser refuses to let a page from one origin read responses from another unless the API opts in
(cross-origin resource sharing). The API ships one CORS policy, built from the `Cors` configuration
section (`ApiCorsOptions`, `Api/Cors/`), applied to every endpoint by `UseApiCors()`. It is secure by
default: with no configuration `AllowedOrigins` is empty, no origin is allowed, and no `Access-Control-*`
header is ever sent. `appsettings.json` has an empty list; only `appsettings.Development.json` lists the
localhost dev servers (`http://localhost:5173`, `:4200`, `:3000`).

## Options

| Setting | Default | Notes |
| --- | --- | --- |
| `AllowedOrigins` | empty (nothing allowed) | Each entry is `scheme://host[:port]` exactly as a browser sends it: lowercase, `http` or `https`, no path, query, fragment, user info or trailing slash, default port omitted. No duplicates. |
| `AllowedMethods` | `GET`, `POST`, `PUT`, `PATCH`, `DELETE` | Method tokens; at least one when set. |
| `AllowedHeaders` | `Authorization`, `Content-Type`, `If-Match`, `Accept` | Request headers a preflight may ask for; at least one when set. `Content-Type` is needed because `application/json` and `application/merge-patch+json` are not CORS-safelisted. |
| `ExposedHeaders` | `ETag`, `Link`, `X-Total-Count`, `X-Trace-Id`, `Location`, `Retry-After`, `api-supported-versions` | See below. An empty list exposes nothing. |
| `AllowCredentials` | `false` | See the warning below. |
| `PreflightMaxAgeSeconds` | `600` | 0 to 86400; how long a browser may cache a preflight answer. |

Every rule is validated when the host starts (`[OptionsValidator]` plus `CorsOriginListAttribute` and
`CorsTokenListAttribute`), so a malformed value stops startup instead of silently never matching. A
configured method, header or exposed-header list replaces the default list; it is not appended to it.

## No wildcard origin

`*` is rejected outright, in every list: an origin is always named explicitly. A wildcard origin
would let any site on the internet script the API from a visitor's browser, and it cannot be combined
with credentials anyway. Allowing "every subdomain" or "any localhost port" is the same mistake in a
smaller size, so those patterns are refused too.

## The exposed-headers contract

A browser hides every response header that is not CORS-safelisted from page scripts unless the
response lists it in `Access-Control-Expose-Headers`. The default list is therefore the contract with a
browser client, and `ApiCorsOptions.DefaultExposedHeaders` is its only definition (a test checks that
this table and the tables above name every default):

| Header | Emitted by | Why a client needs it |
| --- | --- | --- |
| `ETag` | `GET`, `POST`, `PUT`, `PATCH` on one product | The value to send back in `If-Match`. |
| `Link` | `GET /api/v1/products` | Next and previous page URLs (RFC 8288). |
| `X-Total-Count` | `GET /api/v1/products` | The total number of matches. |
| `X-Trace-Id` | Every response | The id to quote when reporting a problem. |
| `Location` | `POST /api/v1/products` | The URL of the created product. |
| `Retry-After` | Every `429` from the [rate limiter](#rate-limiting-a-budget-per-caller) | How long to wait before retrying. |
| `api-supported-versions` | Every versioned response | The API versions the server offers. |

## Where it sits in the pipeline, and why

```text
forwarded headers (only with trusted proxies) -> routing (implicit) -> TraceIdMiddleware
  -> Serilog request logging -> exception handler -> status-code pages -> HTTPS redirection -> CORS
  -> authentication -> user log context -> impersonation audit -> request timeout -> rate limiter -> authorization -> endpoint
```

CORS runs after routing and before authentication (and before the rate limiter, which a preflight therefore
never meets; see [Rate limiting](#rate-limiting-a-budget-per-caller)). A preflight is a browser-sent `OPTIONS` request with
`Access-Control-Request-Method` that, by design, carries no `Authorization` header. If authentication
and the fallback authorization policy ran first they would answer it with a `401`, and the browser would
refuse every cross-origin call that needs a preflight (all writes and every call with an
`Authorization` header). CORS answers the preflight itself and never calls the rest of the pipeline;
`tests/.../CorsTests.cs` proves it against the real pipeline, including the fallback policy. CORS runs
inside the trace-id middleware, request logging and the exception handler, so a preflight, a request
from a refused origin and a request that fails later all still carry `X-Trace-Id` and appear in the
request log. Nothing is special-cased: health endpoints and, in Development, the OpenAPI and Scalar
documents get the same policy as the API.

The policy allows a preflight to be answered with the full allowed method and header lists rather than
refusing a disallowed one; the browser compares them with what the page asked for and blocks the call.
With more than one allowed origin the response carries `Vary: Origin` (the framework omits it when a
single origin is configured).

## Allowing a new origin

Add it to the list in configuration for the environment that needs it, for example the environment
variable `Cors__AllowedOrigins__0=https://app.example.com` (increment the index for each further origin),
or the `Cors:AllowedOrigins` array in that environment's settings file. Do not commit a production origin
to `appsettings.Development.json`.

## Warning: `AllowCredentials`

`AllowCredentials: true` lets scripts from the allowed origins send cookies and other browser-held
credentials. The API authenticates with a bearer token that the client attaches itself, so it does not
need this. Enable it only if a cookie-based client is added, and then only with a short, trusted origin
list and CSRF protection in place: an allowed origin can act with the user's ambient credentials.

# Rate limiting: a budget per caller

The API limits how fast one caller can call it, with the framework's own rate limiter
(`Microsoft.AspNetCore.RateLimiting`, in the shared framework; no package). It is on by default and bounded
by default: the limits below apply with no configuration at all. Registration and placement live in
`Api/RateLimiting/` (`AddApiRateLimiting()`, `UseApiRateLimiting()`), so `Program.cs` stays a list of calls.

## Options

`RateLimitingOptions` (section `RateLimiting`) holds one budget per policy. Each budget is at most
`PermitLimit` requests per caller in each fixed window of `WindowSeconds` seconds; a request over the limit is
refused at once, never queued, unless `QueueLimit` says otherwise.

| Policy | PermitLimit | WindowSeconds | QueueLimit | Applies to |
| --- | --- | --- | --- | --- |
| `Reads` | 120 | 60 | 0 | Every `GET`, and any action that names no policy (see below) |
| `Writes` | 30 | 60 | 0 | `POST`, `PUT`, `PATCH` and `DELETE` on products |
| `Impersonation` | 5 | 60 | 0 | `POST /api/v1/impersonation/tokens`: minting a credential |

`PermitLimit` is 1 to 1,000,000, `WindowSeconds` 1 to 86,400 and `QueueLimit` 0 to 1,000. Every rule is
validated when the host starts (`[OptionsValidator]`, nested per policy), so a zero or absurd value stops
startup. The defaults are the same in code and in `appsettings.json`; a test checks that, and that this table
lists them. Override per environment, for example `RateLimiting__Writes__PermitLimit=100`.

**Fixed window, and why.** A fixed window is one counter per caller per window: the cheapest algorithm, the
easiest to explain to a client, and the one whose `Retry-After` is exact (the time until the window rolls
over). Its known weakness is a burst of up to twice the limit across a window boundary; for these limits that
is acceptable, and a sliding window or a token bucket can replace it in one place (`PartitionFor`) if a client
needs smoother pacing. `QueueLimit` stays 0 because a queued request holds a connection open, which makes the
queue a resource an attacker can fill.

**Restart to change a limit.** The limits are read once, through `IOptions`. Live reload through
`IOptionsMonitor` was tried and rejected: a limit is a security control, and once an invalid value was written
to a watched settings file the monitor's current value threw, so every caller arriving afterwards was answered
`500` (the reload itself throws too, as it does for every validated options class in this host). With `IOptions`
a configuration reload, valid or not, changes nothing until the next start: the limiter keeps the limits it
started with, and the next start refuses an invalid value.

## Who is counted

Each policy keeps one counter per caller (a "partition"), so callers never share a budget and the three
policies never share one either:

- An **authenticated** caller is counted by identity: the key is `user:` plus the token's `sub`. A token with
  no `sub` has no identity and is counted by address.
- An **impersonated** request (a token that carries an `act` claim) is counted against the **real actor**,
  never the identity it runs as. Otherwise an administrator could mint a token for each of a hundred users and
  spend a fresh budget under each. The actor is read with the same `GetActorId()` the audit stream uses; a token
  whose actor cannot be read falls back to the address, never to the effective identity. The `Impersonation`
  policy uses the same rule, so it is always the real caller who is limited.
- An **anonymous** caller, including one whose token was rejected, is counted by address: `ip:` plus the
  connection's remote address (an IPv4-mapped IPv6 address is unwrapped; a connection with no address shares
  the fixed key `ip:unknown`, so it is one bucket, not an exemption).

The kind prefix means an address string can never collide with a user id (a user whose id is `203.0.113.7`
does not share the budget of the address `203.0.113.7`; a test proves it). The keys are never logged or
returned: a refusal logs only the policy and the kind (`user` or `ip`), and the user id is already a property of
every log event. An address is never in a problem body.

## Every endpoint is limited unless it says otherwise

- Every controller action is limited. `[EnableRateLimiting(RateLimitPolicyNames.Writes)]` puts the four
  mutating actions on `Writes` and `[EnableRateLimiting(RateLimitPolicyNames.Impersonation)]` the token
  endpoint. An action with **no** attribute gets `Reads`: `MapControllers().WithDefaultRateLimiting()` adds it
  as endpoint metadata only when the action declared nothing, so a declared policy always wins and a forgotten
  attribute costs a looser budget, never no budget. A new mutating action should still say `Writes`.
- **Exempt:** `/health/live` and `/health/ready` (a probe must never be refused), and in Development the
  OpenAPI and Scalar endpoints. Each says `DisableRateLimiting()` where it is mapped, so an exemption is always
  visible in the code.
- A test walks every endpoint the host maps and fails if one carries neither `EnableRateLimiting` nor
  `DisableRateLimiting`, or if a controller action's policy does not fit its verb; another adds a throwaway
  controller with an unannotated action and proves it is limited.
- **Not counted:** a request that matches no endpoint (an unknown route) has no policy to apply, and a CORS
  preflight is answered before the limiter (below). Neither does any work.

**Exempting a future endpoint.** Say so where it is declared: `[DisableRateLimiting]` on the action, or
`.DisableRateLimiting()` on a mapped endpoint, and add its route to the exempt list in
`EveryEndpoint_IsLimitedOrExplicitlyExempt_Test`, which is the guard against an exemption nobody chose. A
non-controller endpoint that should be limited needs `.RequireRateLimiting(...)` (or
`.WithDefaultRateLimiting()`); the same test fails until it has one or the other.

## Where it sits in the pipeline

```text
forwarded headers (only with trusted proxies) -> routing (implicit) -> TraceIdMiddleware
  -> Serilog request logging -> exception handler -> status-code pages -> HTTPS redirection -> CORS
  -> authentication -> user log context -> impersonation audit -> request timeout -> rate limiter -> authorization -> endpoint
```

- After **authentication**, because the partition is the caller and the principal must exist.
- After **CORS**, because a preflight is answered by CORS and never reaches the limiter: it neither spends
  budget nor is refused when the budget is spent (a test proves both). CORS also adds its headers before the
  request continues, so a `429` to an allowed origin carries them, and `Retry-After` is on the exposed list, so
  browser code can read how long to wait (a test asserts it on a real `429` with an `Origin`).
- Before **authorization**, so a request the fallback policy answers `401` (or a policy `403`) still spends
  budget: an attacker guessing tokens is limited too.
- After the **impersonation audit**, so a request made under an impersonation token that the limiter refuses is
  still recorded (as a `429`) against the real actor.
- After the **request timeout**, so a request waiting in the limiter's queue (`QueueLimit` above 0) is bounded by the
  deadline too.

## The `429` response

```http
HTTP/1.1 429 Too Many Requests
Content-Type: application/problem+json
Retry-After: 42
X-Trace-Id: 4bf92f3577b34da6a3ce929d0e0e4736

{
  "type": "https://tools.ietf.org/html/rfc6585#section-4",
  "title": "Too Many Requests",
  "status": 429,
  "detail": "Rate limit exceeded. Retry after 42 seconds.",
  "code": "RATE_LIMITED",
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736"
}
```

It is a problem body like every other non-2xx response: `traceId` and `X-Trace-Id` agree, and `code` is the
stable member a client matches on (as `NOT_FOUND` is for a 404). `Retry-After` is whole seconds, taken from the
limiter's own hint (the time until the window rolls over); when the limiter gives none, the policy's whole
window is used. The body says nothing about other callers, the limit or the address. A refusal is logged once
at `Warning` (`RateLimited`, event id 1300) with the policy and the partition kind, and is declared on every
operation of the OpenAPI document with the `Retry-After` header and this example.

**A refused impersonation-token request is also audited**, closing the gap "every attempt is audited, including
denials": the request never reaches the command, so the rejection handler writes an `Impersonation.IssueToken`
event with outcome `RateLimited` (actor from the principal, or none for an anonymous caller; source address;
trace id). It is best effort: a failure to write is logged at `Error` (event id 1301) and the `429` still goes
out, because refusing the request is the point. A refusal by any other policy is not audited.

## Behind a reverse proxy

Behind a load balancer every connection comes from the proxy, so without help every anonymous caller would
share one address and one budget. `ForwardedHeaders:TrustedProxies` (`ApiForwardedHeadersOptions`,
`Api/Proxies/`) opts in:

```json
{ "ForwardedHeaders": { "TrustedProxies": ["10.0.0.5", "10.1.0.0/16"] } }
```

- **Empty (the default): the forwarded-headers middleware is not enabled at all.** A client's `X-Forwarded-For`
  is ignored, so it cannot choose the address it is limited under (a test sends a different spoofed value on
  every request and stays in one partition).
- **With entries:** `UseForwardedHeaders` runs first in the pipeline and honours `X-Forwarded-For` and
  `X-Forwarded-Proto` only from those addresses or networks (the framework's default trust of loopback is
  removed). The client address a trusted proxy reports then feeds the rate limiter, the request log and the
  audit `sourceIp`; from any other sender the headers are ignored.
- Each entry is an IP address or a CIDR network, validated on start: a malformed entry, a network whose address
  has host bits set (`10.0.0.1/24`), an unspecified address and a catch-all network (`0.0.0.0/0`, `::/0`) stop
  the host, because trusting everyone is the same as trusting the client.
- Only the nearest proxy hop is read (`ForwardLimit` 1). A chain of proxies needs each hop listed.

## Limitations

- **Per instance, in memory.** Each instance counts on its own, so the effective limit is the configured one
  times the number of instances, and a restart clears every counter. Enforcing one limit across instances needs
  a gateway in front of them or a shared store (a distributed limiter); this POC does neither.
- **IPv6 callers** are counted by full address, so one client holding a whole `/64` can rotate addresses.
  Counting by prefix is a small change to `RateLimitCaller` if that matters.
- **Anonymous callers behind one NAT share a budget**, and so do all of them when the proxy is not trusted (see
  above): configure the proxy.
- **Memory:** a partition exists per caller seen; the framework discards a fully replenished, idle partition.

# Request timeouts: a deadline per request

Every request has a deadline. When it passes, the request's `HttpContext.RequestAborted` token is cancelled, the
handler's `CancellationToken` (which already flows from the controller through every MediatR behavior, handler
and EF Core call) stops what it is doing, and the caller is answered `504 Gateway Timeout`. The mechanism is the
framework's own (`Microsoft.AspNetCore.Http.Timeouts`, in the shared framework; no package). It is on by default
and bounded by default: the deadlines below apply with no configuration at all. Registration and placement live
in `Api/RequestTimeouts/` (`AddApiRequestTimeouts()`, `UseApiRequestTimeouts()`), so `Program.cs` stays a list of
calls.

## Options

`RequestTimeoutOptions` (section `RequestTimeouts`) holds one deadline per policy, as a `TimeSpan`
(`hh:mm:ss`, or `hh:mm:ss.fff`).

| Setting | Default | Applies to |
| --- | --- | --- |
| `RequestTimeouts:Default` | `00:00:30` | Every endpoint that names no policy and does not opt out |
| `RequestTimeouts:Impersonation` | `00:00:10` | `POST /api/v1/impersonation/tokens` |

Each value must be from one millisecond to ten minutes (`[TimeoutRange]`), validated when the host starts
(`[OptionsValidator]`), so a zero, negative or absurd value stops startup. The defaults are the same in code and in
`appsettings.json`; a test checks that, and that this table lists them. Override per environment, for example
`RequestTimeouts__Default=00:01:00`. Sub-second values exist so tests can use tens of milliseconds; they are never
sensible in production. Like the rate limits, the values are read once through `IOptions`: a change takes a
restart.

**Why a separate, shorter policy for minting a token.** The endpoint does one signature and one audit append, so
a healthy call takes milliseconds. A stalled audit store or key provider should fail that endpoint fast rather than
hold connections of the most sensitive endpoint open for the whole default. Nothing else is different about it.

## Every endpoint is covered unless it says otherwise

- The framework applies the default policy to every endpoint that carries no `[RequestTimeout("policy")]` and no
  `[DisableRequestTimeout]`, so an action added later with no attribute is covered without anyone remembering to
  do anything. A named policy always wins over the default.
- **Exempt:** `/health/live` and `/health/ready` (the orchestrator owns a probe's deadline, and a `504` from the API
  would misreport a slow dependency as the probe's own failure) and, in Development, the OpenAPI and Scalar
  endpoints (documents are generated on first request and are not part of the API's traffic). Each says
  `DisableRequestTimeout()` where it is mapped, so an exemption is always visible in the code.
- A test walks every endpoint the host maps and fails if a controller action is exempt, if any action other than
  the token endpoint names a policy, or if an exempt endpoint is not one of the operational ones above; another
  adds a throwaway controller with an unannotated action, and one that names a policy, and proves the first is
  timed out and the second uses its own deadline.

**Exempting a future endpoint.** Say so where it is declared (`[DisableRequestTimeout]` on the action, or
`.DisableRequestTimeout()` on a mapped endpoint) and add its route prefix to the operational list in
`EveryExemptEndpoint_IsOperational_Test`. An endpoint that legitimately needs longer (an export, say) should get
its own named policy rather than an exemption.

## Where it sits in the pipeline

```text
forwarded headers (only with trusted proxies) -> routing (implicit) -> TraceIdMiddleware
  -> Serilog request logging -> exception handler -> status-code pages -> HTTPS redirection -> CORS
  -> authentication -> user log context -> impersonation audit -> request timeout -> rate limiter
  -> authorization -> endpoint
```

- After **routing**, because the policy is chosen from the resolved endpoint's metadata.
- Inside the **exception handler** and request logging, so the `504` is the status they see and report.
- After **CORS**, so a preflight (answered there, instantly) never meets a timer.
- After the **impersonation audit**, so the audit records the real `504` of a timed-out impersonated request. The
  timeout middleware turns the cancellation into the `504` itself; were it outside the audit, the audit would see
  the cancellation as an exception and record `500`.
- Before the **rate limiter**, **authorization** and the action, so the deadline covers a request waiting in the
  limiter's queue (`QueueLimit` above 0), the authorization policies and the handler. It starts after
  **authentication**, which validates a JWT locally with no I/O and so cannot stall.

## The `504` response

```http
HTTP/1.1 504 Gateway Timeout
Content-Type: application/problem+json
X-Trace-Id: 4bf92f3577b34da6a3ce929d0e0e4736

{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.6.5",
  "title": "Gateway Timeout",
  "status": 504,
  "detail": "The request did not complete in time and was cancelled.",
  "code": "REQUEST_TIMEOUT",
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736"
}
```

It is a problem body like every other non-2xx response: `traceId` and `X-Trace-Id` agree, `type` comes from
`HttpMappingOptions.TypeUris`, and `code` is the stable member a client matches on. It is written by
`RequestTimeoutResponseWriter`, the policies' `WriteTimeoutResponse`, and is declared on every operation of the
OpenAPI document with an example. A timeout is logged once at `Warning` (`RequestTimedOut`, event id 1400); the
framework's own duplicate line is filtered out (`Microsoft.AspNetCore.Http.Timeouts` is at `Error`), and the
request line for the `504` is a `Warning` too. Nothing is logged at `Error` and `GlobalExceptionHandler` never
sees it.

**The status is `504`, not `408`.** `408 Request Timeout` says the client was too slow sending the request;
here the server ran out of time doing the work, so a client that retries an idempotent request is doing the right
thing and one that retries a `POST` should check the resource first.

## Cancellation: what is and is not interrupted

- **The timeout is cooperative.** It cancels a token; work that honours the token (every handler and EF Core
  call here) stops, and work that ignores it keeps running. A handler that never observes its token is not
  interrupted, and the response is whatever it eventually produces.
- **Timeout versus client abort.** The timeout middleware answers the cancellation only when its own timer fired.
  A client that hangs up cancels the original token instead, and that is still swallowed silently (no `504`, no
  body, no `Error` line) exactly as before; a test proves both, side by side.
- **A change may have happened.** A `504` on `POST`, `PUT`, `PATCH` or `DELETE` means the request was cancelled,
  not that nothing changed: a transactional command cancelled before its commit is rolled back (a test proves a
  `POST` cancelled inside its transaction creates nothing), but a request cancelled after its commit has still
  committed. The `If-Match` precondition makes a retry of `PUT`/`PATCH`/`DELETE` safe.
- **Rollback and logging on cancellation.** `TransactionBehavior` rolls back with `CancellationToken.None`: the
  path runs because the request is cancelled, and a rollback given the cancelled token would be skipped by the
  provider and reported by it as a transaction error. It logs the cancellation at `Information`
  (`... was cancelled; rolling back transaction`), not `Error`; any other exception is still an `Error`. The same
  holds for a client that hangs up.
- **Audit.** For an auditable command cancelled by the timeout, `AuditBehavior` sees the pipeline throw and records
  one event with outcome `Exception` (best effort), then rethrows; the timeout middleware answers `504`. The
  audit write itself deliberately ignores the request's token (a disconnecting client must not cost a committed
  action its record), so the timeout cannot interrupt it either.
- **A timed-out impersonation mint delivers no token.** If the pipeline is cancelled before the command returns,
  the exception propagates, no response body is built, and the attempt is audited as `Exception`; a test asserts a
  `504`, no `token` member anywhere in the body and exactly one audit event. The exception is the pipeline
  cancellation itself, so this holds wherever inside the pipeline the deadline lands.
- **A slow audit write does not turn a finished mint into a `504`.** If the token was already minted and the
  *audit write* is what is slow, nothing interrupts the request (the write ignores the token and the timeout only
  cancels a token): it runs past its deadline, the event is written, and only then is the token returned with a
  `200`. Fail-closed still holds (no token without its event), and a test asserts that order. What the deadline
  cannot do is bound a hung audit store: that needs a timeout on the store itself.
- **Middleware after the timeout that catches `OperationCanceledException` itself** would hide the cancellation
  from the timeout middleware; none does today.

## Debugger caveat

The framework's timeout middleware does nothing while a debugger is attached, so the deadlines never fire in a
debug session and the timeout tests, which assume no debugger, fail if run under one. Run them with
`dotnet test`, not with the debugger attached.

# OpenAPI contract check: no accidental drift

The OpenAPI document is the API's public contract, and it is generated from code, so a change to a controller, a
`ProducesResponseType`, a DTO or a transformer can change it without anyone meaning to. A test compares the
served document with a committed snapshot, so an unintended change fails the build and an intended one is
visible in the same commit's diff.

## How it works

- The snapshot is `tests/MediatrUnionPoc.Api.IntegrationTests/Contracts/openapi.v1.json`: the `v1` document
  (versioned paths only; the transitional unversioned alias is not in the contract).
- `OpenApiContractTests` boots the real host (Development, anonymous, exactly as `/openapi/v1.json` is served to
  the UI), fetches the document and normalizes it: every object's keys in ordinal order, every line ending inside a
  string turned into LF (documentation text carries the line endings of the source files it was compiled from,
  which differ between checkouts), and the `servers` member removed (the test host's address is a property of the
  machine). It compares the result with the snapshot as parsed trees, so line endings and key order in the file on
  disk can never cause a false alarm.
- The test-host approach was chosen over generating the document at build time with
  `Microsoft.Extensions.ApiDescription.Server`: it needs no new package, no build step or extra project, and it
  checks exactly the document a client receives, transformers and API-versioning integration included.
- Other tests assert the document holds versioned paths only, that neither it nor the snapshot contains a
  signing key, a bearer token or a local path, and that the snapshot file is already in canonical form.

## When it fails

The failure message says what changed, not just that something did: a count of added, removed and changed
locations, the operations added or removed, the first 25 differences as readable paths
(`+ paths['/api/v1/widgets'].get`, `- ....responses.404 (was ...)`,
`~ ....properties.price.type: "number" -> "string"`), and the exact command to regenerate the snapshot.
`OpenApiContractComparerTests` proves against a mutated copy of the real document that an added path, a removed
response code and a changed schema property are each detected.

## Updating the snapshot deliberately

When the change is intended, regenerate the file, read its diff and commit it with the code change:

```bash
UPDATE_OPENAPI_SNAPSHOT=1 dotnet test tests/MediatrUnionPoc.Api.IntegrationTests --filter "FullyQualifiedName~OpenApiContractTests"
```

```powershell
$env:UPDATE_OPENAPI_SNAPSHOT=1; dotnet test tests/MediatrUnionPoc.Api.IntegrationTests --filter "FullyQualifiedName~OpenApiContractTests"; Remove-Item Env:UPDATE_OPENAPI_SNAPSHOT
```

- Rewriting happens only when `UPDATE_OPENAPI_SNAPSHOT` is `1` or `true`; it is never on by default.
- It is refused when the `CI` variable is set (the test fails instead), so a pipeline can never rewrite the
  contract it exists to check. CI runs plain `dotnet test`, which needs no setup.
- A second run of the update writes a byte-identical file, and a plain run then passes.
