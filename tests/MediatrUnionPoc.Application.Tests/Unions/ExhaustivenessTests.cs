using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace MediatrUnionPoc.Application.Tests.Unions;

/// <summary>
/// Proves union exhaustiveness is enforced by the compiler — not just claimed in documentation.
/// Each test builds a tiny scratch project under tests/CompileTimeChecks/ (deliberately excluded
/// from MediatrUnionPoc.slnx, so an intentionally broken build never fails `dotnet build`/`dotnet
/// test` at the solution level) with the real, installed SDK compiler — rather than hosting a
/// Microsoft.CodeAnalysis.CSharp NuGet package in-process — which matters here specifically
/// because `union` exhaustiveness analysis is a brand-new C# 15/.NET 11 preview compiler feature;
/// an off-the-shelf Roslyn package could easily predate it and silently fail to reproduce this
/// behavior.
/// </summary>
/// <remarks>
/// In CI, these scratch projects are built by a dedicated workflow step
/// (.github/workflows/ci.yml, "Build CompileTimeChecks scratch projects") that captures each
/// build's exit code and output to obj/ci-build-result.txt next to the project; when that file
/// exists, <see cref="BuildScratchProjectAsync"/> reads it directly instead of spawning a
/// process. Building them from inside the test itself — a child `dotnet build` spawned by
/// `Process.Start` from a process that is itself being driven by a running `dotnet test`/MSBuild
/// build — hangs reliably on GitHub Actions' Linux runners (matches known, unresolved upstream
/// reports of a child `dotnet build` hanging inside MSBuild's own BuildSubmission.Execute() in
/// this exact parent/child topology: dotnet/sdk#42821, #43432, #45461. See
/// docs/research/dotnet-build-child-process-ci-hang.md for the full investigation, including five
/// other fixes that were tried and ruled out before landing on this one). Locally, running a
/// single test or the whole suite via `dotnet test` never reproduces the hang, so
/// <see cref="BuildScratchProjectAsync"/> falls back to spawning `dotnet build` itself whenever
/// the capture file is absent.
/// </remarks>
public class ExhaustivenessTests
{
    /// <summary>
    /// Proves a <c>switch</c> over a union that omits a declared case (no discard arm) fails to
    /// compile with CS8509, rather than merely being flagged by an analyzer or passing silently.
    /// </summary>
    /// <returns>A task that completes when the scratch build finishes and the assertions run.</returns>
    [Fact]
    public async Task Non_exhaustive_switch_over_a_union_fails_to_compile()
    {
        var (exitCode, output) = await BuildScratchProjectAsync("NonExhaustive");

        Assert.NotEqual(0, exitCode);
        Assert.Contains("CS8509", output);
    }

    /// <summary>
    /// Proves the exhaustive counterpart of <see cref="Non_exhaustive_switch_over_a_union_fails_to_compile"/>
    /// — the same union, switched over with every case covered — compiles with no CS8509.
    /// </summary>
    /// <returns>A task that completes when the scratch build finishes and the assertions run.</returns>
    [Fact]
    public async Task Exhaustive_switch_over_the_same_union_compiles_cleanly()
    {
        var (exitCode, output) = await BuildScratchProjectAsync("Exhaustive");

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("CS8509", output);
    }

    /// <summary>
    /// The same proof as <see cref="Non_exhaustive_switch_over_a_union_fails_to_compile"/>, but for
    /// <c>ITransactionOutcome&lt;TSelf&gt;.ShouldCommit</c> specifically — this is what guarantees
    /// commit/rollback can't silently misclassify an arbitrary, previously-unseen case type: the
    /// case must be declared on the union, and the union's own <c>ShouldCommit</c> switch must
    /// classify it, or the build fails.
    /// </summary>
    /// <returns>A task that completes when the scratch build finishes and the assertions run.</returns>
    [Fact]
    public async Task Non_exhaustive_ShouldCommit_switch_fails_to_compile()
    {
        var (exitCode, output) = await BuildScratchProjectAsync("ShouldCommitNonExhaustive");

        Assert.NotEqual(0, exitCode);
        Assert.Contains("CS8509", output);
    }

    /// <summary>
    /// Proves the exhaustive counterpart of
    /// <see cref="Non_exhaustive_ShouldCommit_switch_fails_to_compile"/> — every case classified in
    /// <c>ShouldCommit</c> — compiles with no CS8509.
    /// </summary>
    /// <returns>A task that completes when the scratch build finishes and the assertions run.</returns>
    [Fact]
    public async Task Exhaustive_ShouldCommit_switch_compiles_cleanly()
    {
        var (exitCode, output) = await BuildScratchProjectAsync("ShouldCommitExhaustive");

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("CS8509", output);
    }

    /// <summary>
    /// Gets the exit code and combined output of building one of the scratch projects under
    /// tests/CompileTimeChecks/ — either by reading a CI-captured result (see the type-level
    /// remarks) or, if none exists, by building it directly with the real, installed SDK
    /// compiler.
    /// </summary>
    /// <param name="projectName">
    /// The scratch project's directory and .csproj name under tests/CompileTimeChecks/, e.g.
    /// "NonExhaustive".
    /// </param>
    /// <param name="cancellationToken">A token used to cancel a fallback build early.</param>
    /// <param name="thisSourceFile">
    /// Supplied automatically by the compiler via <see cref="CallerFilePathAttribute"/>; used to
    /// locate tests/CompileTimeChecks/ relative to this file rather than the current working
    /// directory.
    /// </param>
    /// <returns>The build's exit code and its combined standard output and error text.</returns>
    private static async Task<(int ExitCode, string Output)> BuildScratchProjectAsync(
        string projectName,
        CancellationToken cancellationToken = default,
        [CallerFilePath] string thisSourceFile = ""
    )
    {
        // tests/MediatrUnionPoc.Application.Tests/Unions/ExhaustivenessTests.cs -> up to tests/ -> CompileTimeChecks/<projectName>
        var testsDirectory = Path.GetDirectoryName(Path.GetDirectoryName(thisSourceFile))!;
        var projectDirectory = Path.Combine(
            Path.GetDirectoryName(testsDirectory)!,
            "CompileTimeChecks",
            projectName
        );
        var projectPath = Path.Combine(projectDirectory, $"{projectName}.csproj");

        Assert.True(File.Exists(projectPath), $"Scratch project not found: {projectPath}");

        var capturedResultPath = Path.Combine(projectDirectory, "obj", "ci-build-result.txt");
        return File.Exists(capturedResultPath)
            ? ReadCapturedResult(capturedResultPath)
            : await BuildScratchProjectBySpawningAsync(projectPath, cancellationToken);
    }

    /// <summary>
    /// Parses the exit code and output captured by the CI workflow's own build of this scratch
    /// project (first line: exit code; remaining lines: combined stdout/stderr).
    /// </summary>
    /// <param name="capturedResultPath">Path to the captured result file.</param>
    /// <returns>The captured exit code and output.</returns>
    private static (int ExitCode, string Output) ReadCapturedResult(string capturedResultPath)
    {
        var lines = File.ReadAllLines(capturedResultPath);
        Assert.True(lines.Length > 0, $"Captured build result is empty: {capturedResultPath}");
        Assert.True(
            int.TryParse(lines[0], out var exitCode),
            $"Captured build result's first line isn't an exit code: {capturedResultPath}"
        );

        return (exitCode, string.Join(Environment.NewLine, lines[1..]));
    }

    /// <summary>
    /// Builds a scratch project by spawning `dotnet build` directly. Only used when no
    /// CI-captured result exists (see the type-level remarks for why CI itself never takes this
    /// path); reliable in that circumstance, since it is exactly how a local `dotnet test` run
    /// already builds and proves these projects today.
    /// </summary>
    /// <param name="projectPath">Path to the scratch project's .csproj file.</param>
    /// <param name="cancellationToken">A token used to cancel the build early.</param>
    /// <returns>The build process's exit code and its combined standard output and error text.</returns>
    private static async Task<(int ExitCode, string Output)> BuildScratchProjectBySpawningAsync(
        string projectPath,
        CancellationToken cancellationToken
    )
    {
        var startInfo = new ProcessStartInfo(
            "dotnet",
            $"build \"{projectPath}\" --nologo -p:NuGetAudit=false"
        )
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
        };
        startInfo.EnvironmentVariables["DOTNET_CLI_DISABLE_BUILD_SERVERS"] = "1";
        startInfo.EnvironmentVariables["MSBUILDDISABLENODEREUSE"] = "1";
        startInfo.EnvironmentVariables.Remove("MSBUILDUSESERVER");
        startInfo.EnvironmentVariables.Remove("MSBuildSDKsPath");
        startInfo.EnvironmentVariables.Remove("MSBuildExtensionsPath");
        startInfo.EnvironmentVariables.Remove("MSBuildLoadMicrosoftTargetsReadOnly");

        var timeoutDuration = TimeSpan.FromMinutes(3);
        using var timeout = new CancellationTokenSource(timeoutDuration);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeout.Token
        );

        using var process = Process.Start(startInfo)!;
        process.StandardInput.Close();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(linked.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(linked.Token);

        try
        {
            while (!process.HasExited)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200), linked.Token);
            }

            var output = await stdoutTask + await stderrTask;
            return (process.ExitCode, output);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            // A diagnostic snapshot for whoever hits this locally — the CI hang this fallback
            // path was never able to reproduce on a plain `dotnet test` run (see the type-level
            // remarks), so a timeout here means something new.
            var processTree = await CaptureProcessTreeAsync(process.Id);
            process.Kill(entireProcessTree: true);
            Assert.Fail(
                $"`dotnet build \"{projectPath}\"` did not exit within {timeoutDuration} — killed "
                    + $"the process tree. Process tree at time of kill:\n{processTree}"
            );
            throw;
        }
    }

    /// <summary>
    /// Captures a snapshot of <paramref name="rootProcessId"/> and its descendants via the
    /// platform's process-listing tool, for diagnosing a timed-out build. Best-effort: any
    /// failure to capture is folded into the returned text rather than thrown, since this runs
    /// while a test is already failing for a different reason.
    /// </summary>
    /// <param name="rootProcessId">The process ID whose tree should be captured.</param>
    /// <returns>Human-readable process tree output, or a description of why it is unavailable.</returns>
    private static async Task<string> CaptureProcessTreeAsync(int rootProcessId)
    {
        try
        {
            var (command, arguments) = OperatingSystem.IsWindows()
                ? ("wmic", "process get ProcessId,ParentProcessId,CommandLine")
                : ("ps", "-eo pid,ppid,stat,etime,args --forest");

            using var snapshot = Process.Start(
                new ProcessStartInfo(command, arguments)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                }
            )!;
            using var snapshotTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var output = await snapshot.StandardOutput.ReadToEndAsync(snapshotTimeout.Token);
            await snapshot.WaitForExitAsync(snapshotTimeout.Token);

            return $"(root pid {rootProcessId})\n{output}";
        }
        catch (Exception ex)
        {
            return $"failed to capture process tree: {ex}";
        }
    }
}
