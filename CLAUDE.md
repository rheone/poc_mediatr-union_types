# CLAUDE.md

This file provides guidance to AI coding assistants working with code in this repository.

## What this is

A proof of concept testing whether C#'s `union` type (a C# 15 / .NET 11 preview language feature — works as a MediatR/CQRS response type, replacing
thrown exceptions and null with a closed, compiler-exhaustive set of outcomes per operation. See
`README.md` for the full write-up (motivation, design rationale, Mermaid diagrams) — this file only
covers what a session needs to be productive quickly.

## Commands

```bash
dotnet build                                    # build the whole solution
dotnet test                                     # run all tests
dotnet test --filter "FullyQualifiedName~Name"  # run a single test/class by (partial) name
dotnet run --project src/MediatrUnionPoc.Api    # run the API locally
dotnet format whitespace                        # auto-fix whitespace/using-order issues
```

The SDK is pinned via `global.json` to an exact .NET 11 preview build (`allowPrerelease: true`).
Without it, some tools (Visual Studio in particular) fall back to the newest *stable* SDK on the
machine and fail with `NETSDK1045`.

## Architecture

**Solution layout** (`MediatrUnionPoc.slnx`, 4 `src/` projects + 5 `tests/` projects):

- `src/MediatrUnionPoc.Domain` — `Product` entity, Vogen value objects (`ProductId`, `Money`),
  `IProductRepository`/`IUnitOfWork` interfaces. Has no dependency on any other project here.
- `src/MediatrUnionPoc.Application` — commands, queries, handlers, validators, organized as
  **vertical slices** under `Features/Products/<Operation>/` (Create, Update, Delete, GetById,
  GetPaged) rather than by technical layer. `Common/` holds the shared pipeline machinery (below).
- `src/MediatrUnionPoc.Infrastructure` — EF Core (`EfCoreUnitOfWork`, `ProductRepository`,
  hand-written `ValueConverter`s for the Vogen types — not Vogen's own generated converter, to
  keep Domain free of an EF Core reference).
- `src/MediatrUnionPoc.Api` — one controller (`ProductsController`); every action's only job is to
  `switch` on the union MediatR returns and produce an `IActionResult`.

Each `src/` project has its own unit test project, named after it, plus a separate integration test
project wherever tests need a real database, a real HTTP host, or both:

- `tests/MediatrUnionPoc.Domain.Tests` — pure unit tests for `Money`, `ProductId`, and `Product`;
  no dependency on any other project.
- `tests/MediatrUnionPoc.Application.Tests` — xUnit v3 + NSubstitute, organized by what's under test
  (`Unions/`, `Behaviors/`, `Handlers/`, `Validators/`). Handlers are tested against a substituted
  `IProductRepository`, never the real EF Core provider.
- `tests/MediatrUnionPoc.Infrastructure.IntegrationTests` — the one project that exercises the real
  EF Core SQLite provider (an in-memory database on one kept-open connection per test) end-to-end
  (`EfCoreUnitOfWorkTests`), rather than substituting `IUnitOfWork`.
- `tests/MediatrUnionPoc.Api.IntegrationTests` — boots the real ASP.NET Core host via
  `WebApplicationFactory` and exercises it through actual HTTP requests, against the real SQLite
  provider; each host gets its own private in-memory database.
- `tests/MediatrUnionPoc.ArchitectureTests` — `NetArchTest.Rules` assertions on the compiled `src/`
  assemblies enforcing the layering described above.

`tests/CompileTimeChecks/` holds four tiny scratch projects (`Exhaustive`, `NonExhaustive`,
`ShouldCommitExhaustive`, `ShouldCommitNonExhaustive`) **deliberately excluded from the `.slnx`**
— one pair proves a non-exhaustive `switch` over a union is a real `CS8509` compiler error, the
other proves the same for `ITransactionOutcome.ShouldCommit`. `ExhaustivenessTests.cs` shells out
to `dotnet build` against them; that's why those directories live outside the solution file. See
`tests/CompileTimeChecks/README.md` for why it exists and how to manage it (adding a new probe,
keeping it out of the `.slnx`, `.csharpierignore` interaction).

**The core pattern**: every command/query returns a `union` of exactly the outcomes that operation
can produce (e.g. `union CreateProductResult(ProductDto, ValidationErrors, Error)`) and never
throws for an expected outcome (validation failure, not-found, etc.) — the controller's `switch`
is the only place a union gets translated into an HTTP status. See `README.md`'s "Why this
matters" and "Shared case types are meaning-free" sections for the full rationale.

**MediatR pipeline** (registered in `Application/DependencyInjection.cs`, in this exact order):
`LoggingBehavior` → `AuthorizationBehavior` → `ValidationBehavior` → `TransactionBehavior` — who's
calling is checked before whether their input is well-formed. Marker interfaces in
`Application/Common/Abstractions/` control which behaviors apply to which requests:

- `ICommand<TResponse>` — mutates state, no transaction assumption.
- `ITransactionalCommand<TResponse> : ICommand<TResponse>` — opts into `TransactionBehavior`;
  requires the response union to implement `ITransactionOutcome<TResponse>` (a `static abstract
  bool ShouldCommit(TResponse)`, implemented per-union as an exhaustive `switch` over that union's
  own cases).
- `IQuery<TResponse>` — read-only, never wrapped in a transaction.
- `IValidatable<TSelf>` — a union implements this (`static abstract TSelf
  FromValidationErrors(ValidationErrors)`) so `ValidationBehavior` can short-circuit generically
  without knowing the concrete union type.
- `IRequiresAuthorization` — a request implements this (exposing `ClaimsPrincipal Principal` and a
  `string PolicyName`) to opt into `AuthorizationBehavior`, which checks `Principal` against
  whichever ASP.NET Core authorization policy `PolicyName` names (`IAuthorizationService` + a
  custom `IAuthorizationHandler`). Requires the response union to implement
  `IAuthorizable<TResponse>` (a `static abstract TSelf FromNotAuthorized(NotAuthorized)`), the
  same generic short-circuit pattern `IValidatable` uses for validation. See README's
  "Authorization" section for how to configure it and gate a new command behind it.

Case types (`Success`, `NotFound`, `Error`, `ValidationErrors`, `Failure`, `NotAuthorized` — in
`Application/Common/Results/CaseTypes.cs`) are deliberately meaning-free and reused across unions;
a case type's identity never implies what it means for commit/rollback or anything else — only
the union that declares it decides that. That's why `ITransactionOutcome.ShouldCommit` exists
instead of `TransactionBehavior` pattern-matching a fixed list of known case types.

## Conventions specific to this repo

- Every async method this repo owns the signature of ends in `Async` and takes
  `CancellationToken cancellationToken = default`. The one exception is `Handle` on
  `IRequestHandler`/`IPipelineBehavior` implementations — MediatR's interfaces fix that method
  name and require a non-defaulted token, so those can't follow the convention.
- ASP.NET Core's `SuppressAsyncSuffixInActionNames` default (`true`) is overridden to `false` in
  `Program.cs` so controller action names keep the `Async` suffix — otherwise `nameof(GetByIdAsync)`
  used in `CreatedAtAction` silently stops matching the action name MVC would register it under.
- Central Package Management: every package version lives in `Directory.Packages.props`;
  individual `.csproj` files reference packages without a `Version` attribute.
- `.editorconfig` documents, with inline rationale, every analyzer severity override — check it
  before assuming a suppressed StyleCop/SonarAnalyzer rule is an oversight. Most notably: `SA1649`
  is disabled because it crashes outright on any file containing a `union` declaration (the
  analyzer predates the language feature).
- Every public/protected member has an XML doc comment (`GenerateDocumentationFile=true`, `CS1591`
  enabled repo-wide except the `CompileTimeChecks` scratch projects) — this is enforced, not
  aspirational. A new public member without a `<summary>` produces a build warning.
