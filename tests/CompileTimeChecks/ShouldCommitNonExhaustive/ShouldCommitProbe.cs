// TODO: excluded from CSharpier via .csharpierignore (union declarations crash CSharpier 1.3.0's
// parser). To reverse: remove this file's entry from .csharpierignore, run
// `dotnet csharpier check .`, and delete this comment if it passes.
using MediatrUnionPoc.Application.Common.Abstractions;

namespace CompileTimeChecks.ShouldCommitNonExhaustive;

public sealed record Approved;

public sealed record Rejected;

// A brand-new, arbitrary case type — never seen by TransactionBehavior, never seen anywhere else
// in this codebase. Standing in for "a future dev adds a case to their own union."
public sealed record NeedsManualReview;

// ShouldCommit only classifies two of this union's three cases. This must fail to build with
// CS8509 — the whole point of ITransactionOutcome<TSelf> is that this can't compile silently.
public union ApprovalOutcome(Approved, Rejected, NeedsManualReview) : ITransactionOutcome<ApprovalOutcome>
{
    public static bool ShouldCommit(ApprovalOutcome response) => response switch
    {
        Approved => true,
        Rejected => false,
    };
}
