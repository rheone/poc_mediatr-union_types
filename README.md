# MediatrUnionPoc

A proof of concept: can C#'s new [`union`](docs/union-type.md#the-c-union-type) type serve as the response type for a
[MediatR](docs/glossary.md#mediatr-vocabulary)[^mediatr-license]/[CQRS](docs/glossary.md#architectural-patterns) handler, replacing
the usual "throw an exception or return null" grab-bag with a closed, exhaustively-checked set of
outcomes?

Short answer: yes, and it composes well with MediatR pipeline behaviors via generic constraints on
**[static abstract interface members](docs/union-type.md#static-abstract-interface-members-why-generic-code-can-build-a-union-its-never-seen)**.
Details below.

> [!IMPORTANT]
> The `union` keyword is a **C# 15** feature — it needs **.NET 11 Preview 5+**.
> This repo targets `net11.0` with `<LangVersion>preview</LangVersion>` and was built
> against the .NET 11 RC1 SDK.

> [!NOTE]
> **This is one opinionated take, not a prescription.** This repo starts from the position that
> exceptions should be reserved for truly exceptional circumstances — not for ordinary business
> outcomes like "not found" or "validation failed" — and builds a set of suggested conventions
> around that position: unions as response types, meaning-free shared case types, and pipeline-based
> cross-cutting concerns. There are many other defensible ways to solve the same problem — a
> hand-rolled `Result<T>`, a third-party library, or exceptions used deliberately and consistently
> could all work for a different team. Treat what follows as "one thing that works," not "the
> correct answer."

## Getting started

```bash
dotnet build
dotnet test
dotnet run --project src/MediatrUnionPoc.Api
```

Once the API is running, these are the addresses to know (the `https` launch profile also serves the
same paths on `https://localhost:7070`):

| URL | What it is | Auth |
| --- | --- | --- |
| `http://localhost:5233/scalar` | Scalar API UI; paste a bearer token in its Authentication panel to call endpoints (Development only) | Anonymous page |
| `http://localhost:5233/openapi/v1.json` | OpenAPI document for API version 1 (Development only) | Anonymous |
| `http://localhost:5233/api/v1/products` | The Products API (`GET` list, `POST`); `/api/v1/products/{id}` for `GET`, `PUT`, `PATCH`, `DELETE` | Bearer token |
| `http://localhost:5233/api/v1/impersonation/tokens` | `POST` mints a short-lived token that acts as another user (see [Roles and impersonation](#roles-and-impersonation)) | Bearer token with role `Administrator` or `Support` |
| `http://localhost:5233/health/live` | Liveness probe; no checks, `200` while the process responds | Anonymous |
| `http://localhost:5233/health/ready` | Readiness probe; a database round trip, `200` or `503` | Anonymous |

There is no `/health` route; the two probe paths are configurable under `HealthEndpoints`
(see [Health checks and options](docs/operations.md#health-checks-and-options)). Mint a local token as described in
[Authorization](docs/authorization.md#where-the-identity-comes-from).

### Roles and impersonation

There is no user directory or login endpoint: a role exists only as a `role` claim inside a token you
sign yourself. In Development, `dotnet user-jwts` does that with no extra configuration; its `--name`
becomes the token's `sub` (the product owner) and `--role` adds a `role` claim (repeat it for several):

```bash
dotnet user-jwts create --project src/MediatrUnionPoc.Api --name alice                          # a plain user
dotnet user-jwts create --project src/MediatrUnionPoc.Api --name root --role Administrator      # may DELETE, may impersonate
dotnet user-jwts create --project src/MediatrUnionPoc.Api --name sam  --role Support            # may impersonate only
```

To call the API from the Scalar UI (`http://localhost:5233/scalar`, Development only), mint a token as above,
then open **Authentication**, choose **Bearer** (the API declares an HTTP `Bearer` JWT scheme) and paste the
token alone, with no "Bearer" prefix; Scalar sends it on every request. The page itself loads anonymously, but every
call it makes needs the token, and a `401` means the token is missing or expired. Use the same `--name` for
`PUT` and `PATCH`, since only the product's owner (the token's `sub`) may change it, and copy the `ETag` from the
earlier response into their `If-Match` header (for example `W/"1"`). To act as another user, call
`POST /api/v1/impersonation/tokens` from the UI with a `Support` or `Administrator` token, then paste the
returned token into the same field.

Role names are case-sensitive. `Administrator` is required for `DELETE /api/v1/products/{id}`; either
`Administrator` or `Support` is required to mint an impersonation token. Impersonation is enabled in
every environment and is a controlled authentication bypass, so it has its own signing key
(`Impersonation:SigningKey`, which must differ from the JWT key) and a mandatory reason. Every attempt is audited:

```bash
curl -i -X POST http://localhost:5233/api/v1/impersonation/tokens \
  -H "Authorization: Bearer $SUPPORT" -H "Content-Type: application/json" \
  -d '{"targetUserId":"alice","roles":[],"reason":"Reproducing the error alice reported","lifetimeMinutes":15}'
# 200: {"token":"...","expiresAt":"...","userId":"alice",...}; send it as "Authorization: Bearer <token>"
```

An impersonation token lives 15 minutes by default (`Impersonation:DefaultLifetimeMinutes`). A request may ask
for another lifetime with `lifetimeMinutes`, up to `Impersonation:MaxLifetimeMinutes` (60 by default); a larger
value is a `400`. The response's `expiresAt` is the token's exact expiry. Tokens from `dotnet user-jwts` are
separate: the tool sets their expiry (about 90 days by default), not this API.

The target must differ from the caller, `reason` needs 10 to 500 characters, the requested roles must be in
`Impersonation:AssignableRoles` (`Support` and `Administrator` by default), and a `Support` caller cannot
mint a role they do not hold. An impersonation token cannot be used to mint another. Set
`Impersonation:Enabled=false` to turn the endpoint into a `404`. See
[Impersonation](docs/impersonation.md#impersonation-acting-as-another-identity) and
[Authorization](docs/authorization.md#where-the-identity-comes-from).

`dotnet run` uses the first launch profile, so the API listens on `http://localhost:5233` (the
`https` profile adds `https://localhost:7070`). In the Development environment it serves the
OpenAPI document of API version 1 at `/openapi/v1.json` (one document per version) and a Scalar UI at `/scalar`. With no
`ConnectionStrings:Products` value the API keeps a private in-memory SQLite database (empty on every
start); set that value to a SQLite connection string such as `Data Source=products.db` to persist.
Every endpoint except the health probes (and, in Development, the OpenAPI and Scalar documents) requires a bearer token; a quick tour, using a token minted as described under [Authorization](docs/authorization.md#where-the-identity-comes-from):

```bash
curl -i -X POST http://localhost:5233/api/v1/products -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" -d '{"name":"Widget","price":9.99}'   # 201, ETag: W/"1"
curl -i http://localhost:5233/api/v1/products -H "Authorization: Bearer $TOKEN" # 200 with X-Total-Count and Link
curl -i http://localhost:5233/api/v1/products                                   # 401, a problem body
```

The full request/response contract of every endpoint is in
[The HTTP contract](docs/http-contract.md#the-http-contract-every-endpoint-and-outcome); the routes are versioned
(`/api/v1/...`, see [API versioning](docs/http-contract.md#api-versioning)).

`global.json` pins the SDK to the exact `11.0.100-rc.1...` preview build this repo was written
against. Without it, an IDE's own SDK resolver (Visual Studio in particular) can silently fall
back to the newest *stable* SDK it finds and fail with `NETSDK1045` ("does not support targeting
.NET 11.0") — `allowPrerelease: true` is what tells it a preview SDK is expected here, not a
missing `<TargetFramework>` value. If VS still shows the error after pulling this file, close and
reopen the solution so it re-resolves.

### Project layout

| Project                          | Responsibility                                                            |
| --------------------------------- | --------------------------------------------------------------------------- |
| [`MediatrUnionPoc.Domain`](src/MediatrUnionPoc.Domain/README.md)          | Entities, [Vogen](docs/value-objects.md#vogen-avoiding-primitive-obsession) [value objects](docs/glossary.md#vogen-vocabulary) (`ProductId`, `Money`, `ProductVersion`), the listing vocabulary (`ProductCriteria`, `ProductSort`, `PagedResult`), `CommitResult`, repository/UoW interfaces |
| [`MediatrUnionPoc.Application`](src/MediatrUnionPoc.Application/README.md)     | Commands, queries, handlers, union result types, validators, pipeline behaviors, authorization |
| [`MediatrUnionPoc.Infrastructure`](src/MediatrUnionPoc.Infrastructure/README.md)  | EF Core `DbContext` over SQLite, repository + unit-of-work implementations; the only place criteria and sort become a database query |
| [`MediatrUnionPoc.Api`](src/MediatrUnionPoc.Api/README.md)             | The controllers that map each union to an `IActionResult`, the `Http/` extension members, URL-segment API versioning, JWT authentication, impersonation token signing, the file-backed audit stream and its middleware, trace id middleware, exception handler and OpenAPI transformers |
| [`MediatrUnionPoc.Domain.Tests`](tests/MediatrUnionPoc.Domain.Tests/README.md)    | Unit tests for the value objects, `Product`, `ProductNames`, `PagedResult` and the sort vocabulary |
| [`MediatrUnionPoc.Application.Tests`](tests/MediatrUnionPoc.Application.Tests/README.md) | xUnit v3 + NSubstitute — union mechanics, pipeline behaviors, handlers, validators, authorization |
| [`MediatrUnionPoc.Infrastructure.IntegrationTests`](tests/MediatrUnionPoc.Infrastructure.IntegrationTests/README.md) | Real EF Core SQLite provider (in-memory database), end to end |
| [`MediatrUnionPoc.Api.IntegrationTests`](tests/MediatrUnionPoc.Api.IntegrationTests/README.md) | `WebApplicationFactory`-based Api integration tests against real SQLite |
| [`MediatrUnionPoc.ArchitectureTests`](tests/MediatrUnionPoc.ArchitectureTests/README.md) | `NetArchTest.Rules` assertions enforcing the layering above               |

Four one-file probe projects under `tests/CompileTimeChecks/` are deliberately not in the solution;
see [Testing](docs/testing.md#testing) and the [CompileTimeChecks README](tests/CompileTimeChecks/README.md).

Application code is organized as **[vertical slices](docs/glossary.md#architectural-patterns)** under
`Features/Products/<Operation>/` (`Create`, `Update`, `Patch`, `Delete`, `GetById`, `GetPaged`) and
`Features/Impersonation/IssueToken/` — everything
for one operation (command/query, validator, handler, result union) lives together, rather than
being split across horizontal "Commands/Handlers/Validators" folders.

## Motivation

Most CQRS handlers end up with a response shape that's either:

- **A single DTO**, with failures signaled by throwing (`NotFoundException`, `ValidationException`,
  ...) and caught somewhere upstream, or
- **A hand-rolled "Result" wrapper** (`Result<T>`, `OneOf<T1,T2,...>`, `ErrorOr<T>`) from a
  third-party library, because C# had no first-class closed-union type to express
  "exactly one of these N things came back."

`union` gives you option two, built into the language, with compiler-enforced
[exhaustiveness](docs/glossary.md#c-language-concepts) at every [`switch`](docs/glossary.md#c-language-concepts). This project
pushes that as far as it reasonably goes: every command and query returns a `union` of whatever
outcomes are actually possible for that operation — no more, no fewer — and nothing in the request
pipeline throws for an outcome it expected to see.

## What this pattern provides, and its actual scope

The union-as-response convention is this repo's own choice, not a MediatR requirement: `IRequestHandler<TRequest, TResponse>` places no constraint on `TResponse`, and an operation with a single possible outcome has no reason to use a union at all. The full text, including [Union types as responses](docs/scope.md#union-types-as-responses), is in [What this pattern provides, and its actual scope](docs/scope.md#what-this-pattern-provides-and-its-actual-scope).

- Exhaustiveness is checked by the compiler: a new case breaks every `switch` that does not handle it ([The C# `union` type](docs/union-type.md#the-c-union-type)).
- The signature is the contract, and each operation declares exactly its own outcomes ([Case types](docs/case-types.md#case-types-used-here)).
- Generic pipeline code can build a union it has never seen, through [static abstract interface members](docs/union-type.md#static-abstract-interface-members-why-generic-code-can-build-a-union-its-never-seen).

## What this POC demonstrates, and what it leaves out

The full lists are in [What this POC demonstrates, and what it leaves out](docs/scope.md#what-this-poc-demonstrates-and-what-it-leaves-out); in short:

- **Demonstrated**, on one small Products API: `union` responses with compiler-checked exhaustiveness, a generic MediatR pipeline ([request lifecycle](docs/request-lifecycle.md#request-lifecycle)), optimistic concurrency, JSON Merge Patch and paged listing ([the HTTP contract](docs/http-contract.md#the-http-contract-every-endpoint-and-outcome)), uniform RFC 7807 problem bodies and a trace id everywhere ([trace id and unhandled exceptions](docs/logging-and-errors.md#trace-id-and-unhandled-exceptions)).
- **Out of scope**: an identity provider ([where the identity comes from](docs/authorization.md#where-the-identity-comes-from)), cross-instance rate limiting ([rate limiting](docs/operations.md#rate-limiting-a-budget-per-caller)), migrations and a production database ([duplicate product names](docs/concurrency.md#duplicate-product-names)), soft delete and audit columns ([the audit stream](docs/audit.md#audit-stream-a-separate-record-of-security-relevant-actions)), and an exception-tracking service or log server.

## Documentation

The rest of the documentation lives in [`docs/`](docs/index.md), whose index describes every page.

| Group | Pages |
| --- | --- |
| The pattern | [Scope of the pattern and the POC](docs/scope.md) · [The C# `union` type](docs/union-type.md) · [Case types](docs/case-types.md) · [No exceptions for expected outcomes](docs/no-exceptions.md) · [Request lifecycle](docs/request-lifecycle.md) · [Transactions and Unit of Work](docs/transactions.md) · [Vogen value objects](docs/value-objects.md) |
| Guides | [Adding a new command or query](docs/adding-a-command.md) · [Worked example: UpdateProductCommand](docs/worked-example-update.md) · [Extending the pattern: syncing a search index](docs/extending-search-index.md) · [Speculative shared case types](docs/speculative-case-types.md) · [Testing](docs/testing.md) |
| HTTP API | [The HTTP contract and API versioning](docs/http-contract.md) · [Optimistic concurrency](docs/concurrency.md) · [Partial updates: PATCH](docs/patch.md) · [Listing products](docs/listing.md) |
| Security | [Authorization](docs/authorization.md) · [Impersonation](docs/impersonation.md) · [Audit stream](docs/audit.md) |
| Operations | [Trace id, unhandled exceptions and logging](docs/logging-and-errors.md) · [Health checks, CORS, rate limiting, timeouts and the OpenAPI check](docs/operations.md) |
| Reference | [Notes and gotchas](docs/notes-and-gotchas.md) · [Glossary](docs/glossary.md) |
| Projects | Each project has its own README: [Domain](src/MediatrUnionPoc.Domain/README.md) · [Application](src/MediatrUnionPoc.Application/README.md) · [Infrastructure](src/MediatrUnionPoc.Infrastructure/README.md) · [Api](src/MediatrUnionPoc.Api/README.md) · [Domain.Tests](tests/MediatrUnionPoc.Domain.Tests/README.md) · [Application.Tests](tests/MediatrUnionPoc.Application.Tests/README.md) · [Infrastructure.IntegrationTests](tests/MediatrUnionPoc.Infrastructure.IntegrationTests/README.md) · [Api.IntegrationTests](tests/MediatrUnionPoc.Api.IntegrationTests/README.md) · [ArchitectureTests](tests/MediatrUnionPoc.ArchitectureTests/README.md) · [CompileTimeChecks](tests/CompileTimeChecks/README.md) |
| Other | [Features](docs/Features.md) · [Hardening plan](docs/Hardening-Plan.md) · [Documentation split plan](docs/README-Split-Plan.md) · [Research notes](docs/research/) |

[^mediatr-license]: MediatR's own license changed starting with v10 — free for individuals and
    small organizations, commercial licensing applies above a revenue threshold. See
    [MediatR's licensing page](https://github.com/jbogard/MediatR/blob/master/LICENSE.md#other-licenses)
    for current terms before adopting it in anything beyond a POC; they've changed before and may
    change again.
