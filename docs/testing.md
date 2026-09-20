# Testing

Part of the [documentation](index.md).

```bash
dotnet test                                                     # everything
dotnet test tests/MediatrUnionPoc.Api.IntegrationTests          # one project
dotnet test --filter "FullyQualifiedName~Name"                  # a test or class by partial name
dotnet test --filter "FullyQualifiedName!~ExhaustivenessTests"  # skip the slow compiler probes
```

Five test projects sit under `tests/`, one per layer plus the layering rules, all xUnit v3. Each
has its own README.

| Project | What it covers | Persistence |
| --- | --- | --- |
| `MediatrUnionPoc.Domain.Tests` | `Money`, `ProductId`, `ProductVersion`, `Product`, `ProductNames`, `PagedResult`, `ProductSort`; no other project referenced | none |
| `MediatrUnionPoc.Application.Tests` | Union mechanics, the pipeline behaviors and their registration order, every handler, validators, authorization, the audit behavior and event format | `IProductRepository` and `IUnitOfWork` substituted with NSubstitute |
| `MediatrUnionPoc.Infrastructure.IntegrationTests` | `EfCoreUnitOfWork` (commit, rollback, concurrency and unique-violation translation), `ProductRepository` including listing, converters, the EF model | real SQLite, an in-memory database on one kept-open connection per test |
| `MediatrUnionPoc.Api.IntegrationTests` | The real host through `WebApplicationFactory` over actual HTTP: status mapping, authentication (a header-driven test scheme by default, real signed JWTs in dedicated tests), impersonation (roles, escalation, chaining, keys, expiry, off switch, no token in logs), rate limiting (the `429` shape, per-user and per-address budgets, the real actor behind an impersonated token, secure by default, preflights and health never limited, trusted proxies), request timeouts (the `504` shape, secure by default, exempt endpoints, client abort versus timeout, a cancelled impersonation mint and a slow audit write), the OpenAPI contract check (a committed `v1` snapshot and its comparer), the audit stream (files, events, fail-closed and best-effort), `ETag`/`If-Match`, `PATCH`, listing headers, trace id, exception handler, OpenAPI | real SQLite, a private in-memory database per host |
| `MediatrUnionPoc.ArchitectureTests` | `NetArchTest.Rules` assertions on the compiled assemblies (layering, only Infrastructure sees EF Core, only Api sees MVC) | none |

There is no EF Core InMemory provider anywhere: runtime and tests both use SQLite, so transactions,
unique indexes and concurrency tokens behave as they would in production.

**Compile-time proof.** Four one-file projects under `tests/CompileTimeChecks/` (`Exhaustive`,
`NonExhaustive`, `ShouldCommitExhaustive`, `ShouldCommitNonExhaustive`) are kept out of
`MediatrUnionPoc.slnx` on purpose: the `NonExhaustive` pair are supposed to fail to build.
`ExhaustivenessTests` shells out to `dotnet build` against each and asserts the `CS8509` outcome; see
[`tests/CompileTimeChecks/README.md`](../tests/CompileTimeChecks/README.md).
