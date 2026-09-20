// TODO: excluded from CSharpier via .csharpierignore (union declarations crash CSharpier 1.3.0's
// parser). To reverse: remove this file's entry from .csharpierignore, run
// `dotnet csharpier check .`, and delete this comment if it passes.
namespace CompileTimeChecks.NonExhaustive;

public sealed record Created;

public sealed record Invalid;

public sealed record Failed;

// A union local to this probe, deliberately NOT one of the production result unions.
public union ProbeResult(Created, Invalid, Failed);

public static class Probe
{
    // The Failed case is intentionally left unhandled. This must fail to build with CS8509
    // ("the switch expression does not handle all possible values ... it is not exhaustive").
    public static string Describe(ProbeResult result) =>
        result switch
        {
            Created => "ok",
            Invalid => "invalid",
        };
}
