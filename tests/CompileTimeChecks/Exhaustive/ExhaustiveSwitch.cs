// TODO: excluded from CSharpier via .csharpierignore (union declarations crash CSharpier 1.3.0's
// parser). To reverse: remove this file's entry from .csharpierignore, run
// `dotnet csharpier check .`, and delete this comment if it passes.
namespace CompileTimeChecks.Exhaustive;

public sealed record Created;

public sealed record Invalid;

public sealed record Failed;

// A union local to this probe, deliberately NOT one of the production result unions: a probe
// that switches over a real union breaks every time that union gains a case.
public union ProbeResult(Created, Invalid, Failed);

public static class Probe
{
    // Same shape as CompileTimeChecks.NonExhaustive.Probe, but every case is handled.
    public static string Describe(ProbeResult result) =>
        result switch
        {
            Created => "ok",
            Invalid => "invalid",
            Failed => "error",
        };
}
