# MediatrUnionPoc.Domain

The innermost layer: the `Product` entity, Vogen-generated value objects (`ProductId`, `Money`,
`ProductVersion`), and the repository/unit-of-work abstractions (`IProductRepository`,
`IUnitOfWork`) that outer layers implement. Holds no MediatR, ASP.NET Core, or EF Core reference — nothing here knows it's
part of a web API or which database persists it.

Value objects are declared with [Vogen](https://github.com/SteveDunn/Vogen) rather than hand-rolled
wrapper structs, so `ProductId`/`Money` get value equality, parsing, and validation from a source
generator instead of boilerplate — see the repo root README's
[Vogen: avoiding primitive obsession](../../README.md#vogen-avoiding-primitive-obsession) for the
full rationale and its [Glossary](../../README.md#glossary) for any term below that isn't
self-explanatory.

`ProductVersion` is the optimistic-concurrency version: it starts at 1 (`ProductVersion.Initial`),
`Product` advances it with `Version.Next()` on every mutation, and it renders as the weak ETag
`W/"n"` (`ToETag` / `ParseETag`). `IUnitOfWork.CommitAsync` returns a `CommitResult` union
(`Committed`, `ConcurrencyConflict`, `UniqueViolation`; `CommitFailure` is the two failing cases) so
an expected commit refusal is a value, not an exception.

`Product` has two mutation entry points besides `Create`: `UpdateDetails(name, price)` replaces both
fields, and `ApplyChanges(name, price)` applies a partial update (a `null` argument means "leave
that field alone", so it stays free of any Application type such as `Optional<T>`; supplying
neither throws). Each advances `Version` exactly once per call, and neither touches `Id`,
`OwnerId` or `CreatedAt`.

`ProductNames.Normalize` (trim, then upper-case with the invariant culture) is the single definition
of when two product names are duplicates; `Product.NormalizedName` carries the key and
`IProductRepository.ExistsWithNameAsync` compares by it. Persistence puts its unique index on the
same key, so the up-front check and the database cannot disagree. See the repo root README's
[Optimistic concurrency](../../README.md#optimistic-concurrency-productversion-etag-and-if-match).

The listing vocabulary is database-agnostic: `ProductCriteria` (optional `NameContains`, `MinPrice`,
`MaxPrice`, `OwnerId`, combined with AND), `ProductSort` (a `ProductSortField` enum allowlist of
`Name`, `Price`, `CreatedAt` plus a `SortDirection`; `ProductSort.Default` is name ascending) and
`PagedResult<T>`, which carries the applied sort and is the one place the page arithmetic lives
(`TotalPages`, `FirstPage`, `LastPage`, `NextPage`, `PreviousPage`). `IProductRepository.GetPagedAsync`
takes the paging, criteria and an ordered list of sort keys; every listing ends in an implicit `Id`
tiebreaker so pages are stable. `Product.CreatedAt` is supplied by the caller (Application reads a
`TimeProvider`); the Domain never reads a clock. `Money` defines the relational operators so
persistence can compare and order it.

## Dependencies

**Project references:** none — this is the dependency graph's root.

**Key packages:**

- `Vogen` — source-generates the strongly-typed value objects.

Also carries the repo-wide analyzer package set (`AsyncFixer`, `IDisposableAnalyzers`,
`Microsoft.VisualStudio.Threading.Analyzers`, `SonarAnalyzer.CSharp`, `StyleCop.Analyzers`) that
every `src/`/`tests/` project (other than the `tests/CompileTimeChecks` scratch probes) carries;
see `.editorconfig` for the severities configured for them.

```mermaid
flowchart LR
    Domain[MediatrUnionPoc.Domain]:::here
    Application[MediatrUnionPoc.Application]
    Infrastructure[MediatrUnionPoc.Infrastructure]
    Api[MediatrUnionPoc.Api]
    DomainTests[MediatrUnionPoc.Domain.Tests]
    AppTests[MediatrUnionPoc.Application.Tests]
    InfraIT[MediatrUnionPoc.Infrastructure.IntegrationTests]
    ApiIT[MediatrUnionPoc.Api.IntegrationTests]
    ArchTests[MediatrUnionPoc.ArchitectureTests]

    Application --> Domain
    Infrastructure --> Domain
    Infrastructure --> Application
    Api --> Domain
    Api --> Application
    Api --> Infrastructure
    DomainTests --> Domain
    AppTests --> Application
    AppTests --> Domain
    AppTests --> Infrastructure
    InfraIT --> Domain
    InfraIT --> Infrastructure
    ApiIT --> Api
    ApiIT --> Application
    ApiIT --> Domain
    ApiIT --> Infrastructure
    ArchTests --> Api
    ArchTests --> Application
    ArchTests --> Domain
    ArchTests --> Infrastructure

    classDef here fill:#ffefc2,stroke:#c98a00,stroke-width:2px;
```

## Usage

This project is a plain class library — nothing runs it directly. It's consumed by
`MediatrUnionPoc.Application` (which builds commands/queries around `Product` and the repository
interfaces), and, via `ProjectReference`, transitively by every other project in the solution.

When changing the `Product` entity or the Vogen value objects, check
`src/MediatrUnionPoc.Infrastructure` for the hand-written EF Core `ValueConverter`s — they're not
generated from Vogen's own converter support, specifically so this project stays free of an EF
Core reference; a shape change here usually means a matching change there.
