# ShouldCommitExhaustive

Positive control for the `ITransactionOutcome<TSelf>.ShouldCommit` exhaustiveness proof —
`ShouldCommitProbe.cs` implements `ShouldCommit` as a `switch` over a union's cases with every
case classified, and is expected to build cleanly with no `CS8509`.

It exists so a build failure in the sibling `ShouldCommitNonExhaustive` project can be attributed
specifically to its missing case, not to something else about the isolated-project setup.

See `tests/CompileTimeChecks/README.md` for why this project — and its three siblings — are kept
out of `MediatrUnionPoc.slnx`, and how they're consumed by
`tests/MediatrUnionPoc.Application.Tests/Unions/ExhaustivenessTests.cs`.

## Dependencies

**Project references:** `MediatrUnionPoc.Application` — for `ITransactionOutcome<TSelf>` and the
union whose cases `ShouldCommit` classifies. No package references, and (see
`Directory.Build.props`) no `GenerateDocumentationFile` requirement.

`ShouldCommitProbe.cs` is listed in `.csharpierignore` and so skipped by the formatter
— CSharpier 1.3.0 can't parse a file that declares a `union`.

## Usage

Never built as part of `dotnet build`/`dotnet test` at the solution level. Built only via
`dotnet build tests/CompileTimeChecks/ShouldCommitExhaustive/ShouldCommitExhaustive.csproj`, which
`ExhaustivenessTests.cs`'s `Exhaustive_ShouldCommit_switch_compiles_cleanly` test does for you.
Don't add it to `MediatrUnionPoc.slnx`.
