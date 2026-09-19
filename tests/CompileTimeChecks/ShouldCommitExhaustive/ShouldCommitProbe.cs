// TODO: excluded from CSharpier via .csharpierignore (union declarations crash CSharpier 1.3.0's
// parser). To reverse: remove this file's entry from .csharpierignore, run
// `dotnet csharpier check .`, and delete this comment if it passes.
using MediatrUnionPoc.Application.Common.Abstractions;

namespace CompileTimeChecks.ShouldCommitExhaustive;

public sealed record Approved;

public sealed record Rejected;

public sealed record NeedsManualReview;

// Same shape as CompileTimeChecks.ShouldCommitNonExhaustive.ApprovalOutcome, but every case —
// including the arbitrary, previously-unseen NeedsManualReview — is classified.
public union ApprovalOutcome(Approved, Rejected, NeedsManualReview) : ITransactionOutcome<ApprovalOutcome>
{
    public static bool ShouldCommit(ApprovalOutcome response) => response switch
    {
        Approved => true,
        Rejected => false,
        NeedsManualReview => false,
    };
}
