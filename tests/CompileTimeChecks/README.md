# tests/CompileTimeChecks

Four tiny, one-file scratch projects that exist to prove a claim this repo makes in prose
(`README.md`, `CLAUDE.md`) is actually enforced by the compiler: that a non-exhaustive `switch`
over a `union` — or over `ITransactionOutcome<TSelf>.ShouldCommit` specifically — is a real
`CS8509` build error, not just a convention or an analyzer suggestion.

## Why this exists

Anyone can *say* "the compiler won't let you forget a case." The only way to actually verify that
for a preview-only C# 15 / .NET 11 language feature is to feed the compiler code that violates it
and check it refuses to build. `tests/MediatrUnionPoc.Application.Tests/Unions/ExhaustivenessTests.cs` does
exactly that: it shells out to the real, installed `dotnet build` (not an in-process
`Microsoft.CodeAnalysis.CSharp` package, which could easily predate `union` support and silently
fail to reproduce this behavior) against each project below and asserts on the exit code and
`CS8509` in the output.

| Project | Proves |
|---|---|
| `Exhaustive` | A `switch` covering every case of a union compiles cleanly (positive control). |
| `NonExhaustive` | The same union, missing one case, fails with `CS8509`. |
| `ShouldCommitExhaustive` | Same proof, for a `static abstract bool ShouldCommit(TSelf)` implementation (`ITransactionOutcome<TSelf>`) that covers every case. |
| `ShouldCommitNonExhaustive` | The same `ShouldCommit` switch, missing one case, fails with `CS8509`. |

The `Exhaustive`/`ShouldCommitExhaustive` "positive control" projects matter as much as the
"negative" ones: without them, a `NonExhaustive` failure could just as easily mean "this isolated
project doesn't build for some unrelated reason" as "the missing case was caught."

## Why it's excluded from `MediatrUnionPoc.slnx`

`NonExhaustive` and `ShouldCommitNonExhaustive` are *supposed* to fail to build. If any of these
four projects were in the solution file, `dotnet build`/`dotnet test` at the solution level would
break on every clean checkout and in CI. Keeping them out of the `.slnx` means they only ever
build when `ExhaustivenessTests.cs` explicitly invokes `dotnet build` against one `.csproj` path
directly — never as a side effect of building the solution.

This is also why `Directory.Build.props` special-cases them out of `GenerateDocumentationFile`
(they're one-file compiler probes, not part of the documented codebase — and one of them is
meant not to build at all), and why `.csharpierignore` excludes the two `ShouldCommitProbe.cs`
files (CSharpier 1.3.0 can't parse `union` declarations at all — see the comment there and in
`.editorconfig`'s `SA1649` override for the same gap in other tooling).

## How it's managed

- **Never add these projects to `MediatrUnionPoc.slnx`.** If you see one there, that's a mistake —
  remove it. (`git diff`/`git log` on the `.slnx` is the fastest way to check whether that's
  happened.)
- **Adding a fifth scratch project**: give it its own directory under here, one `.csproj`, one
  source file, and wire a new `[Fact]` in `ExhaustivenessTests.cs` calling
  `BuildScratchProjectAsync("<YourProjectName>")` — the helper resolves
  `tests/CompileTimeChecks/<projectName>/<projectName>.csproj` relative to that test file via
  `[CallerFilePath]`, so the directory name and `.csproj` name must match exactly.
- **If a new probe's source file declares a `union`**, add its path to `.csharpierignore` up
  front (same pattern as the existing `ShouldCommitProbe.cs` entries) — otherwise `dotnet
  csharpier check .` fails CI with a parser crash, not a formatting diff.
- **Don't add `<GenerateDocumentationFile>`/XML-doc requirements here.** `Directory.Build.props`
  already excludes anything under `CompileTimeChecks` from that repo-wide rule; these are
  intentionally undocumented, minimal probes.
- **Removing this directory** is only appropriate if `ExhaustivenessTests.cs` is also removed (or
  rewritten to stop asserting on real compiler behavior) — the two are inseparable. Removing one
  without the other leaves either dead scratch projects or a test suite that can't build its
  fixtures.

## Known gotcha: build-server hangs under `dotnet test`

`BuildScratchProjectAsync` sets `DOTNET_CLI_DISABLE_BUILD_SERVERS=1` before shelling out. Without
it, `dotnet build` spawned as a child of an already-running `dotnet test` process can hang
indefinitely trying to reuse an MSBuild build-server node tied to the parent's session —
reproducible every time from inside the test, never when run standalone from a shell. There's also
a 3-minute timeout per scratch build as a backstop; a timeout there is almost always a one-off
NuGet restore stall on first build, and reruns succeed.
