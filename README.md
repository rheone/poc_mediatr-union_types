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

> [!TIP]
> **Want to add an endpoint?** Follow [Adding a new API endpoint, step by step](docs/adding-an-endpoint.md):
> every file to add or touch, in order, with the union, validation, authorization, transaction, tests and
> the OpenAPI snapshot.

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

> [!NOTE]
> There is no `/health` route; only the two probe paths above exist, and they are configurable under `HealthEndpoints`
> (see [Health checks and options](docs/operations.md#health-checks-and-options)). The Scalar UI and OpenAPI document are
> mapped only when `ASPNETCORE_ENVIRONMENT` is `Development` (the launch profiles set it).

Mint a local token as described in [Authorization](docs/authorization.md#where-the-identity-comes-from), or use the
[helper scripts](#the-manage-api-helper-scripts) below, or skip tokens with the [development identity](#the-development-identity-no-token-at-all).

### The manage-api helper scripts

[`manage-api.ps1`](manage-api.ps1) (PowerShell 5.1 or 7) and [`manage-api.sh`](manage-api.sh) (bash: Git Bash, WSL,
macOS, Linux; needs `curl` and `openssl`) are equivalent. They wrap the everyday local loop: start and stop the API,
check its health, and list, get, create and delete products as a chosen user. Both need the .NET SDK from `global.json`.

```powershell
./manage-api.ps1 start                                   # run the API on http://localhost:5233 in the background, wait until healthy
./manage-api.ps1 health                                  # GET /health/live and /health/ready
./manage-api.ps1 list                                    # first page (page 1, 10 per page) as alice
./manage-api.ps1 list  -Page 2 -PageSize 5               # any page; prints X-Total-Count and the Link header
./manage-api.ps1 write -User bob -Name "Gadget" -Price 12.5   # POST /api/v1/products as bob (bob becomes the owner)
./manage-api.ps1 get                                     # the product the last write created (or -Id <guid>)
./manage-api.ps1 delete                                  # DELETE it; -User defaults to root (Administrator)
./manage-api.ps1 token -User root                        # print a bearer token, e.g. to paste into the Scalar UI
./manage-api.ps1 set-user -User root                     # tokenless requests become root, live (see the development identity below)
./manage-api.ps1 clear-user                              # tokenless requests are refused (401) again
./manage-api.ps1 status                                  # is something listening, and which process
./manage-api.ps1 stop                                    # stop whatever listens on the port
./manage-api.ps1 restart
```

The bash script takes the same commands, with `--user`, `--id`, `--page`, `--page-size`, `--name`, `--price`, `--port`
and `--token-minutes` instead of the PowerShell parameters:

```bash
./manage-api.sh start
./manage-api.sh write --user bob --name Gadget --price 12.5
./manage-api.sh list --page 2 --page-size 5
./manage-api.sh delete            # the last written product, as root
./manage-api.sh set-user --user root
./manage-api.sh clear-user
./manage-api.sh stop
```

| Command | What it does |
| --- | --- |
| `start` | Runs `dotnet run` in the background, logging to `.dev/api.log` (git-ignored), and returns once `/health/live` answers (up to 120 s while it builds) |
| `stop` | Finds the process listening on the port and ends it with its `dotnet run` parent |
| `restart`, `status` | Stop then start; report whether the port is in use and by which process |
| `health` | Calls `/health/live` and `/health/ready` and prints each status |
| `list` (alias `read`) | Lists products, paged |
| `get` | Fetches one product; prints the `ETag` too |
| `write` | Creates a product; prints `201`, the `ETag` and `Location`, and remembers the new id |
| `delete` | Deletes a product; needs `Administrator`, so the default user is `root` (`-User bob` shows the `403`) |
| `token` | Prints a bearer token for the user |
| `set-user` | Switches the [development identity](#the-development-identity-no-token-at-all) to `-User` (`alice`, `bob`, `root` or `sam`), so requests with no token are that user; needs the user named explicitly |
| `clear-user` | Removes it, so requests with no token are `401` again |

**Defaults**, so a bare command does something useful:

| Option | Default |
| --- | --- |
| user (`-User` / `--user`) | `alice`; `root` for `delete` |
| page, page size (`-Page`, `-PageSize`) | `1` and `10` (page size 1 to 100) |
| id (`-Id` / `--id`) for `get` and `delete` | the product the last `write` created (saved in `.dev/last-product-id`) |
| name, price for `write` | `Widget-<time>` and `9.99` |
| port (`-Port` / `--port`) | `5233`; applies to every command, so a second instance can run on another port |
| token lifetime (`-TokenMinutes` / `--token-minutes`) | 60 minutes |

**The users.** There is no user directory: these four names are only what the scripts put in a token, and any name
works in a token you sign yourself.

| User | Roles | Can |
| --- | --- | --- |
| `alice` | none | list, get, create; change (`PUT`/`PATCH`) only products she owns |
| `bob` | none | the same as `alice`, with his own products |
| `root` | `Administrator` | everything a plain user can, plus `DELETE`, plus minting an impersonation token |
| `sam` | `Support` | everything a plain user can, plus minting an impersonation token, but not `DELETE` |

> [!WARNING]
> The tokens are signed with the **development-only** key in `src/MediatrUnionPoc.Api/appsettings.Development.json`.
> That is why they work at once, with no `dotnet user-jwts` step and no restart, and why they are accepted by a
> Development host only. Never use this key, or these scripts, against a shared or production deployment.

To try the ownership rules: `./manage-api.ps1 write -User bob`, then `PUT` or `PATCH` that product as `alice` (`403`) and as
`bob` (allowed), or `./manage-api.ps1 delete -User bob` (`403`) versus `./manage-api.ps1 delete` as `root` (`204`). The
scripts cover list, get, create and delete; use the Scalar UI or `curl` (with `./manage-api.ps1 token -User <name>`) for
`PUT` and `PATCH`. If PowerShell blocks the script, run `powershell -ExecutionPolicy Bypass -File ./manage-api.ps1 <command>`.

### The development identity: no token at all

For quick testing you can skip tokens entirely. In Development, when `Authentication:DevIdentity:UserId` names a
user, a request that sends no `Authorization` header is signed in as that user with `Authentication:DevIdentity:Roles`.
Scalar, `curl` and a browser then just work.

```json
{
  "Authentication": {
    "DevIdentity": { "UserId": "root", "Roles": ["Administrator"] }
  }
}
```

Both settings default to `null`, which leaves it off. Put your values in the git-ignored
`src/MediatrUnionPoc.Api/appsettings.Development.local.json` (copy
[`appsettings.Development.local.example.json`](src/MediatrUnionPoc.Api/appsettings.Development.local.example.json)).
The API watches that file, so **saving a change applies to the next request with no restart**: set `"alice"` with no
roles to test as a plain user, `"root"` with `["Administrator"]` to test as an administrator, or remove the values to
turn it off again. The quickest way to switch is the scripts: `./manage-api.ps1 set-user -User root` (or
`./manage-api.sh set-user --user root`) writes those two values for you, and `clear-user` removes them. They use their own
git-ignored file, `appsettings.Development.devuser.json`, loaded after the local file so it wins, which lets a script replace
or delete it whole without touching your hand-edited settings. The same user and role names as [the users above](#the-manage-api-helper-scripts) apply, and role
names are case-sensitive.

> [!WARNING]
> This is a second way to sign in, so it is fenced in. It only ever works in the Development environment: a host in any
> other environment **refuses to start** with `UserId` set and ignores a value that appears later. A request that
> does send an `Authorization` header is judged as usual, so a real, expired or invalid token is never replaced. Every
> tokenless sign-in is logged as a warning (event id 1500). See
> [Authorization](docs/authorization.md#the-development-identity-skipping-the-token-in-development).

The local file can also hold other per-developer settings (a persistent `ConnectionStrings:Products`, a longer
`RequestTimeouts:Default` for debugging, higher `RateLimiting` limits, a `Debug` log level that applies live); see
[Health checks and options](docs/operations.md#local-development-settings-per-developer-overrides) and [logging](docs/logging-and-errors.md).

### Starting, finding and stopping the API

Start it in a terminal you keep open (`dotnet run --project src/MediatrUnionPoc.Api`) and stop it with
**Ctrl+C** in that terminal, or with **Stop** (Shift+F5) if you started it from Visual Studio or VS Code, or use
`./manage-api.ps1 start` and `stop` (`./manage-api.sh` on bash).

> [!IMPORTANT]
> The API reads its configuration once at startup. **Restart it after** minting your first `dotnet user-jwts` token
> or changing any setting; a running instance does not know a signing key created after it started, and answers
> `401` with `The signature key was not found`. (Tokens from the helper scripts and the development identity do not have this problem.)

If a port is already in use, or you cannot find the terminal, discover the process that owns it (`5233` is the
`http` profile, `7070` the `https` one):

```powershell
# PowerShell: which process listens on 5233, then stop it
Get-NetTCPConnection -LocalPort 5233 -State Listen | Select-Object LocalPort, OwningProcess
Stop-Process -Id <OwningProcess>
```

```bash
# Git Bash / WSL / macOS / Linux
netstat -ano | grep :5233        # Windows (Git Bash): the last column is the PID; stop it with: taskkill //F //PID <pid>
lsof -i :5233 && kill <pid>      # macOS / Linux
```

To check that the instance you reach is the one you expect, call `http://localhost:5233/health/live` (anonymous,
`200` while it responds). For a `401`, run the request with `curl -i` and read the `WWW-Authenticate` header: it
names the reason, for example `The signature key was not found` (the API started before the token was minted, so
restart it) or an expired token.

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
`Administrator` or `Support` is required to mint an impersonation token.

> [!WARNING]
> Impersonation is enabled in **every** environment and is a controlled authentication bypass: whoever can call it can
> become anyone. It has its own signing key (`Impersonation:SigningKey`, which must differ from the JWT key), a mandatory
> reason, and every attempt is audited. Set `Impersonation:Enabled=false` to remove it.

A request looks like this:

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

The full documentation lives in [`docs/`](docs/index.md), whose index describes every page. The groups below follow
a suggested reading order: each builds on the ones above it, so read top to bottom the first time, then jump to
what you need.

**On this page:** [The pattern](#the-pattern) · [Guides](#guides) · [HTTP API](#http-api) · [Security](#security) · [Operations](#operations) · [Reference](#reference) · [Project documentation](#project-documentation) · [Other documents](#other-documents)

### The pattern

1. [Scope of the pattern and the POC](docs/scope.md)
2. [The C# `union` type](docs/union-type.md)
3. [Case types](docs/case-types.md)
4. [No exceptions for expected outcomes](docs/no-exceptions.md)
5. [Request lifecycle](docs/request-lifecycle.md)
6. [Transactions and Unit of Work](docs/transactions.md)
7. [Vogen value objects](docs/value-objects.md)

### Guides

1. **[Adding a new API endpoint, step by step](docs/adding-an-endpoint.md)**: the start-here checklist for a new endpoint
2. [Adding a new command or query](docs/adding-a-command.md)
3. [Worked example: UpdateProductCommand](docs/worked-example-update.md)
4. [Extending the pattern: syncing a search index](docs/extending-search-index.md)
5. [Speculative shared case types](docs/speculative-case-types.md)
6. [Testing](docs/testing.md)

### HTTP API

1. [The HTTP contract and API versioning](docs/http-contract.md)
2. [Optimistic concurrency](docs/concurrency.md)
3. [Partial updates: PATCH](docs/patch.md)
4. [Listing products](docs/listing.md)

### Security

1. [Authorization](docs/authorization.md)
2. [Impersonation](docs/impersonation.md)
3. [Audit stream](docs/audit.md)

### Operations

1. [Trace id, unhandled exceptions and logging](docs/logging-and-errors.md)
2. [Health checks, CORS, rate limiting, timeouts and the OpenAPI check](docs/operations.md)

### Reference

1. [Notes and gotchas](docs/notes-and-gotchas.md)
2. [Glossary](docs/glossary.md)

### Project documentation

Each project has its own README, in dependency order (source projects first, then their tests):

1. [Domain](src/MediatrUnionPoc.Domain/README.md)
2. [Application](src/MediatrUnionPoc.Application/README.md)
3. [Infrastructure](src/MediatrUnionPoc.Infrastructure/README.md)
4. [Api](src/MediatrUnionPoc.Api/README.md)
5. [Domain.Tests](tests/MediatrUnionPoc.Domain.Tests/README.md)
6. [Application.Tests](tests/MediatrUnionPoc.Application.Tests/README.md)
7. [Infrastructure.IntegrationTests](tests/MediatrUnionPoc.Infrastructure.IntegrationTests/README.md)
8. [Api.IntegrationTests](tests/MediatrUnionPoc.Api.IntegrationTests/README.md)
9. [ArchitectureTests](tests/MediatrUnionPoc.ArchitectureTests/README.md)
10. [CompileTimeChecks](tests/CompileTimeChecks/README.md)

### Other documents

1. [Features](docs/Features.md)
2. [Hardening plan](docs/Hardening-Plan.md)
3. [Documentation split plan](docs/README-Split-Plan.md)
4. [Research notes](docs/research/)

[^mediatr-license]: MediatR's own license changed starting with v10 — free for individuals and
    small organizations, commercial licensing applies above a revenue threshold. See
    [MediatR's licensing page](https://github.com/jbogard/MediatR/blob/master/LICENSE.md#other-licenses)
    for current terms before adopting it in anything beyond a POC; they've changed before and may
    change again.
