# Exhaustive

Positive control for the union-exhaustiveness proof. `ExhaustiveSwitch.cs` switches over
`CreateProductResult` (from `MediatrUnionPoc.Application`) covering every declared case
(`ProductDto`, `ValidationErrors`, `Error`), and is expected to build cleanly with no `CS8509`.

It exists so a build failure in the sibling `NonExhaustive` project can be attributed specifically
to its missing case, not to something else about the isolated-project setup (a bad reference, a
missing package, an SDK mismatch).

See `tests/CompileTimeChecks/README.md` for why this project — and its three siblings — are kept
out of `MediatrUnionPoc.slnx`, and how they're consumed by
`tests/MediatrUnionPoc.Application.Tests/Unions/ExhaustivenessTests.cs`.

## Dependencies

**Project references:** `MediatrUnionPoc.Application` — for `CreateProductResult` and its case
types. No package references, and (see `Directory.Build.props`) no `GenerateDocumentationFile`
requirement — it's a one-file compiler probe, not part of the documented codebase.

## Usage

Never built as part of `dotnet build`/`dotnet test` at the solution level. Built only via
`dotnet build tests/CompileTimeChecks/Exhaustive/Exhaustive.csproj`, which
`ExhaustivenessTests.cs`'s `Exhaustive_switch_over_the_same_union_compiles_cleanly` test does for
you. Don't add it to `MediatrUnionPoc.slnx`.
