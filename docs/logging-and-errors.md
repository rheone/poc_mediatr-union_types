# Trace id and unhandled exceptions

Part of the [documentation](index.md).

Every request has one trace id: the W3C trace id of `Activity.Current` when present (so it joins
to distributed traces), otherwise `HttpContext.TraceIdentifier`, read through the single
`HttpContext.TraceId` extension member. It is exposed three ways, always with the same value:

- the `traceId` member of every `application/problem+json` body, including the framework's own
  (model-binding 400, routing 404, 415, unhandled 500), through `AddProblemDetails` +
  `CustomizeProblemDetails` and `UseStatusCodePages`;
- an `X-Trace-Id` response header on every response, success included (success bodies are
  unchanged);
- a `TraceId` logging scope opened by `TraceIdMiddleware`, which Serilog surfaces as a real
  `TraceId` property, so `LoggingBehavior` and every other log event in the request carry it (see
  [Logging](#logging)).

Expected outcomes are union cases; anything else is a bug or an outage, and `GlobalExceptionHandler`
(`IExceptionHandler` + `UseExceptionHandler`) answers it with a 500 problem body: generic title,
`traceId`, and `detail` (the exception text) **only in the Development environment**. The exception
is logged once at Error with structured properties (the request line that follows for the 500 is a
Warning, never a second Error). There is deliberately no per-exception-type status mapping. A client abort (`OperationCanceledException` while `RequestAborted` is cancelled)
is swallowed with no body and no error-level log.

Serilog is the logging implementation (see below), so exceptions land in the console and the rolling
JSON file with the trace id as a property. Seq is not included; because sinks are configuration,
adding one later needs no code change. No exception-tracking service is bundled: OpenTelemetry
(vendor-neutral exception events on spans) and Sentry remain the options, joined to the logs by the
same trace id.

## Logging

`ILogger` is the only logging API in the code. Serilog is wired behind it in the Api host only
(`UseApiLogging()` in `Api/Logging/`; Application and Domain reference no Serilog, and an
architecture test keeps it so). The logger is built per host from that host's configuration, not
stored in the static `Log.Logger`, so several hosts in one process do not replace each other's.

**Configuration** is the `Serilog` section of `appsettings*.json`, so levels and sinks change without
code. The default level is `Information` with `Microsoft.AspNetCore` and `Microsoft.EntityFrameworkCore`
at `Warning`. The old `Logging:LogLevel` section is not used; Serilog's `MinimumLevel` is the one
place levels are set.

| Sink | Format | Where |
| --- | --- | --- |
| Console | Human-readable line in Development (`[time LVL] traceId userId category: message`); compact JSON elsewhere | standard output |
| File | Compact JSON, one event per line, rolling daily, 14 files kept | `logs/log-<yyyyMMdd>.jsonl` (relative to the working directory; `logs/` is git-ignored) |

**Enrichers.** Every event carries `Application`, `ApplicationVersion`, `Environment` (the host's
environment name), `MachineName`, `ProcessId`, `ThreadId` and whatever the log context holds. Per
request, once the caller is authenticated, it also carries `UserId` (the `NameIdentifier` claim),
`IsImpersonated` and, for an impersonation token, `ImpersonatedBy` (the real caller from the `act`
claim). An anonymous request has none of the three. `TraceId` comes from the `TraceIdMiddleware`
scope. The request log line (`HTTP GET /api/v1/products responded 200 in 12.3456 ms`, replacing the
framework's multi-line request logs) adds `RequestMethod`, `RequestPath`, `StatusCode`, `Elapsed`,
`TraceId` and the same user properties. Health probes are written at `Debug`, so they are silent at
the default level. The request line sits outside the exception handler and logs a handled 500 at
`Warning`, so an unhandled exception is one `Error` entry, written by `GlobalExceptionHandler`.

**Event ids.** The hot-path messages are source-generated `[LoggerMessage]` methods with stable ids:
`LoggingBehavior` 1000 (`Handling {RequestName}`) and 1001 (`Handled {RequestName} -> {ResultCase}`),
`GlobalExceptionHandler` 2000 (unhandled exception) and 2001 (client abort), `AuditBehavior` 1100 and the
audit middleware 1200 (an audit event that could not be written: the action and event id only, never the
event). Security-relevant actions are not recorded here but in the separate
[audit stream](audit.md#audit-stream-a-separate-record-of-security-relevant-actions), which no logging sink receives.

**Never logged.** Tokens, `Authorization` headers, and request or response bodies. Only the three user
properties above are read from the principal. Tests assert that no captured event contains a bearer
token, an impersonation token or an `Authorization` header.

**Reading a trace id in the file logs.** Every response carries `X-Trace-Id`. Find the request with
that value in the `TraceId` property (compact JSON also has the same id as `@tr` when a W3C activity
exists):

```bash
rg '"TraceId":"4bf92f3577b34da6a3ce929d0e0e4736"' logs/
```

That returns the request line and every log event written while it ran, including the exception.
