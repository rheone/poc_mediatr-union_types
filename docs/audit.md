# Audit stream: a separate record of security-relevant actions

Part of the [documentation](index.md).

An audit record answers "who did what, to what, and with what result", years later, for someone who was
not there. That is a different job from diagnostic logging, so it is a different stream: it is never
sampled, never filtered by level, never written to the console or the operational log file, and it is
honest about failure (below). It is deliberately simple, with no new infrastructure: JSON Lines files,
behind an abstraction that a database table can replace later.

## What is recorded

| Action | Written by | Outcome |
| --- | --- | --- |
| `Impersonation.IssueToken` | `AuditBehavior`, for `IssueImpersonationTokenCommand` | The runtime union case: `ImpersonationToken`, `NotAuthorized`, `ValidationErrors`, `Error` |
| `Impersonation.IssueToken` | `RateLimitRejectionHandler`, when the [rate limiter](operations.md#rate-limiting-a-budget-per-caller) refuses a request to the token endpoint before it reaches the pipeline | `RateLimited` (best effort: a failed write is logged at `Error` and the `429` is still sent) |
| `Product.Create`, `Product.Update`, `Product.Patch`, `Product.Delete` | `AuditBehavior`, for the four product mutations | The case: `ProductDto`, `Success`, `NotFound`, `ValidationErrors`, `NotAuthorized`, `PreconditionFailed`, `Conflict`, `Error` |
| `Impersonation.Request` | `ImpersonationAuditMiddleware`, for every HTTP request made under an impersonation token | The HTTP status code |

Reads (`GET`) and requests made with ordinary tokens are not audited, and health probes (anonymous) never
appear. **Not audited:** an anonymous request or a failed authentication (a missing, expired or badly
signed token) never reaches a principal to attribute, so the framework's `401` is only in the operational
request log (the one exception is an anonymous request the rate limiter refuses on the token endpoint, which
is recorded with no actor); a `429` on any other endpoint is only in the operational log; and `POST /api/v1/impersonation/tokens` answering `404` because impersonation is switched off
happens before the pipeline.

## The event

One JSON object per line, camelCase, `null` members omitted (`AuditEvent`, serialized by `AuditEventJson`):

| Member | Meaning |
| --- | --- |
| `id`, `timestamp` | A GUID, and the UTC time from the injectable `TimeProvider` |
| `action`, `outcome` | The stable dotted action name; the union case name (or the status code for `Impersonation.Request`; `Exception` when the rest of the pipeline threw) |
| `actorId` | The **real** caller: the `act` subject when the principal is impersonated, else its own id |
| `effectiveId`, `isImpersonated` | The identity the request ran as, and whether it ran under an impersonation token |
| `tokenId` | The `jti` of an impersonation token, both when it is minted and on every request made with it |
| `targetType`, `targetId` | What the action was aimed at (`User`/`Product` and its id) |
| `reason`, `ticket` | The impersonation reason and ticket reference |
| `traceId` | The same value as the response's `X-Trace-Id`, joining the record to the operational log |
| `sourceIp` | The connection's remote address (behind a reverse proxy, the proxy's unless forwarded headers are enabled) |
| `details` | Small string facts: `roles` granted or asked for, `denial`, `validation`, `errorCode`, and for a request event `method` and `path` (never the query string) |

A token string, a secret, an `Authorization` header or a request body is never part of an event, and
free text is cut to 512 characters (input is audited before it is validated). JSON escapes line breaks
and quotes, so a `reason` containing a newline and a pasted fake record stays one line; a test proves it.

```json
{"id":"2a0f...","timestamp":"2026-03-02T14:05:00+00:00","action":"Impersonation.IssueToken","outcome":"ImpersonationToken","actorId":"sam","effectiveId":"sam","isImpersonated":false,"tokenId":"9c1e...","targetType":"User","targetId":"alice","reason":"Reproducing the checkout error alice reported","ticket":"SUP-1234","traceId":"4bf92f35...","sourceIp":"203.0.113.7","details":{"roles":"Support"}}
```

## How it works

- **`IAuditLog`** (`Application/Common/Auditing/`) is the seam: `Task RecordAsync(AuditEvent, CancellationToken)`.
  Implementations must throw when they cannot store the event. **`FileAuditLog`** (`Api/Audit/`) is the
  implementation: `audit-yyyyMMdd.jsonl`, one file per UTC day chosen by the event's timestamp, appended
  under a lock so concurrent requests never interleave, written through to disk before the call returns.
  (A Serilog sink was not used: sinks swallow write failures, which defeats fail-closed auditing.)
- **`IAuditableRequest<TResponse>`** (`Application/Common/Abstractions/`) is the opt-in marker: an
  `AuditAction`, an `AuditFailurePolicy`, the `AuditPrincipal` to attribute, and
  `DescribeAudit(TResponse)`. **`AuditBehavior`** asks the request to describe itself and the response it
  got and adds who, when, the outcome name, the trace id and the source address (from
  `IAuditRequestContext`, which the Api implements over `HttpContext`, so Application stays free of
  ASP.NET).
- **The request describes its own response.** A create has no id until it succeeds, so
  `CreateProductCommand.DescribeAudit` reads the id from the `ProductDto` case of its own union. Case
  types stay meaning-free: the behavior never interprets a case, it only reads its runtime name for the
  outcome and lets the request, which owns its union, say what the case means for the target.
- **Order.** `AuditBehavior` is registered right after `LoggingBehavior`, so it wraps `AuthorizationBehavior`
  and `ValidationBehavior` and sees their short-circuit outcomes; `TransactionBehavior` is inside it, so a
  product event is written after the commit and reports its real result (a `Conflict` or a
  `PreconditionFailed` is recorded as such).
- **Impersonated requests.** `ImpersonationAuditMiddleware` sits after `UseAuthentication` and before
  `UseAuthorization`, so a `403` an impersonated caller receives is recorded too. A request that ends in an
  unhandled exception is recorded as `500`.

## Failure policy

| Policy | Used by | If the event cannot be written |
| --- | --- | --- |
| `FailClosed` | `Impersonation.IssueToken` | The failure is logged at Error (event id 1100) and an `AuditWriteFailedException` is thrown: the client gets the ordinary `500` problem, and the token the handler had signed is discarded, never delivered. An audit fault is an infrastructure fault, not an expected outcome, so it is an exception like a database outage rather than a union case |
| `BestEffort` | The product mutations and `Impersonation.Request` | The failure is logged at Error (1100 or 1200, the action and event id only) and the response proceeds |

The product mutations are best effort because `TransactionBehavior` sits inside `AuditBehavior`: by the time
the event is written the change has already committed, and failing the request would tell the client that
nothing happened when something did. An operator must therefore watch for those Error events; an unwritable
audit directory is a fault to alert on. The write ignores the request's cancellation token so a client that
disconnects cannot cost a committed action its record.

## Files, retention and configuration

`Audit` section, `AuditOptions`, validated on start: `Directory` (default `logs/audit`, resolved against the
content root; created on the first write). There is deliberately no `Enabled` switch: auditing cannot be
turned off. **The application never deletes, rotates or rewrites an audit file**; retention, archiving and
backup are an operational decision (unlike the operational log, which keeps 14 files). Keep the directory
apart from the operational logs, restrict who can read and write it, and ship it to storage the application
cannot alter if tamper-resistance matters; a file the process can write is a file an intruder with the
process's rights can edit.

## Audit versus logging

| | Operational log | Audit stream |
| --- | --- | --- |
| Question | What is the system doing? | Who did what to what? |
| Level and sampling | Filtered by configured levels | Every event, always |
| Sinks | Console and rolling file through Serilog, configurable | One writer behind `IAuditLog`, not configurable off |
| Write failure | Sinks swallow it | Fail closed, or logged at Error for best-effort actions |
| Content | Diagnostics; never tokens or bodies | Actor, target, outcome, reason; never tokens, secrets or bodies |
| Retention | Rolling, 14 files | Never deleted by the app |

The two are joined only by `traceId`; a test asserts that no audit content reaches the operational log
capture and that the audit files hold none of the operational fields.

## Replacing the file with a table

`IAuditLog` is the only thing the pipeline knows. A database implementation (an `AuditEvents` table with the
same members as columns, `details` as JSON, insert-only permissions for the application's database user)
replaces `FileAuditLog` in `AddAudit` and changes no caller. It would also make the trail queryable and give
tamper-resistance the files cannot. It is not built.
