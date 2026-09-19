# NonExhaustive

Negative control for the union-exhaustiveness proof. `NonExhaustiveSwitch.cs` switches over
`CreateProductResult` (from `MediatrUnionPoc.Application`) but deliberately omits the `Error`
case, with no discard arm. **This project is supposed to fail to build** — that failure, with a
real `CS8509` ("the switch expression does not handle all possible values ... it is not
exhaustive") from the actual installed compiler, is the whole point.

See `tests/CompileTimeChecks/README.md` for why this project — and its three siblings — are kept
out of `MediatrUnionPoc.slnx`, and how they're consumed by
`tests/MediatrUnionPoc.Application.Tests/Unions/ExhaustivenessTests.cs`.

## Dependencies

**Project references:** `MediatrUnionPoc.Application` — for `CreateProductResult` and its case
types. No package references, and (see `Directory.Build.props`) no `GenerateDocumentationFile`
requirement.

## Usage

Never built as part of `dotnet build`/`dotnet test` at the solution level — if it were in
`MediatrUnionPoc.slnx`, every clean checkout would fail to build. Built only via
`dotnet build tests/CompileTimeChecks/NonExhaustive/NonExhaustive.csproj`, which
`ExhaustivenessTests.cs`'s `Non_exhaustive_switch_over_a_union_fails_to_compile` test does for you,
asserting on the non-zero exit code and `CS8509` in its output. Don't add it to
`MediatrUnionPoc.slnx`.
