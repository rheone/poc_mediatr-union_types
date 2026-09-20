# MediatrUnionPoc.Domain.Tests

Unit tests for `MediatrUnionPoc.Domain` in isolation — no MediatR, no FluentValidation, no EF Core,
no mocking framework. Just the Vogen value objects, the entity and the Domain's listing vocabulary:

- `MoneyTests.cs` — verifies `Money`'s `Validate` rejects negative amounts and accepts everything
  else.
- `ProductIdTests.cs` — verifies `ProductId`'s `Validate` rejects `Guid.Empty`, and that
  `ProductId.New()` always produces a valid, non-empty id.
- `ProductTests.cs` — verifies `Product.Create` assigns a fresh, distinct id per call, and
  `Product.UpdateDetails` fully replaces name and price without touching identity; `CreatedAt` is
  the supplied instant and survives an update.
- `PagedResultTests.cs` — the page arithmetic with hand-worked examples (25 items at size 10 is 3
  pages; no previous on page 1; no next on the last; an empty result has one empty page).
- `ProductSortTests.cs` — the default sort and the wire names of the sort vocabulary.
- `ProductVersionTests.cs` — the version's start value, `Next()`, and the weak `ETag` round trip
  (`ToETag` / `ParseETag`, including what is rejected).
- `ProductNamesTests.cs` — `ProductNames.Normalize`, the single definition of a duplicate name.
- `ProductApplyChangesTests.cs` — `Product.ApplyChanges` (the partial-update entry point): which
  fields change, one version bump per call, and what it never touches.

## Dependencies

**Project references:** `MediatrUnionPoc.Domain` only — this project has no dependency on
`Application`, `Infrastructure`, or `Api`, matching Domain's own position as the innermost layer.

**Key packages:**

- `xunit.v3` / `xunit.runner.visualstudio` — test framework (xUnit v3; the test project is an executable) and VSTest runner.
- `coverlet.collector` / `Microsoft.NET.Test.Sdk` — coverage collection and the `dotnet test` host.

Also carries the repo-wide analyzer package set (`AsyncFixer`, `IDisposableAnalyzers`,
`Microsoft.VisualStudio.Threading.Analyzers`, `SonarAnalyzer.CSharp`, `StyleCop.Analyzers`), and a
global `Using Include="Xunit"` so test files don't each need `using Xunit;`.

```mermaid
flowchart LR
    Domain[MediatrUnionPoc.Domain]
    DomainTests[MediatrUnionPoc.Domain.Tests]:::here
    Application[MediatrUnionPoc.Application]
    Infrastructure[MediatrUnionPoc.Infrastructure]
    Api[MediatrUnionPoc.Api]

    Application --> Domain
    Infrastructure --> Domain
    Infrastructure --> Application
    Api --> Domain
    Api --> Application
    Api --> Infrastructure
    DomainTests --> Domain

    classDef here fill:#ffefc2,stroke:#c98a00,stroke-width:2px;
```

## Usage

```bash
dotnet test tests/MediatrUnionPoc.Domain.Tests
```

Runs as part of the normal suite too — `dotnet test` at the solution root includes this project.
