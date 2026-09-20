# ShouldCommitNonExhaustive

Negative control for the `ITransactionOutcome<TSelf>.ShouldCommit` exhaustiveness proof —
`ShouldCommitProbe.cs` implements `ShouldCommit` as a `switch` over a union's cases but
deliberately omits one, with no discard arm. **This project is supposed to fail to build** — that
failure, with a real `CS8509` from the actual installed compiler, is the whole point. It's what
guarantees commit/rollback can't silently misclassify a case a union declares: the union's own
`ShouldCommit` switch must classify every one of its cases, or the build fails.

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

Never built as part of `dotnet build`/`dotnet test` at the solution level — if it were in
`MediatrUnionPoc.slnx`, every clean checkout would fail to build. Built only via
`dotnet build tests/CompileTimeChecks/ShouldCommitNonExhaustive/ShouldCommitNonExhaustive.csproj`,
which `ExhaustivenessTests.cs`'s `Non_exhaustive_ShouldCommit_switch_fails_to_compile` test does
for you, asserting on the non-zero exit code and `CS8509` in its output. Don't add it to
`MediatrUnionPoc.slnx`.
