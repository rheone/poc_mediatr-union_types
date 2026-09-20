using System.Diagnostics;

namespace MediatrUnionPoc.Domain;

/// <summary>The commit succeeded: everything staged since the transaction began is now persisted.</summary>
[DebuggerDisplay("Committed")]
public sealed record Committed;

/// <summary>
/// The commit was refused because a row it touches was changed (or removed) by someone else after
/// this unit of work loaded it — the optimistic-concurrency check failed. Nothing was persisted.
/// </summary>
[DebuggerDisplay("ConcurrencyConflict")]
public sealed record ConcurrencyConflict;

/// <summary>
/// The commit was refused because it would have broken a uniqueness constraint. Nothing was
/// persisted.
/// </summary>
[DebuggerDisplay("UniqueViolation")]
public sealed record UniqueViolation;

/// <summary>
/// The ways a commit can fail for an ordinary, expected reason (as opposed to an unexpected fault,
/// which is thrown). Every transactional response union classifies each of these itself, through
/// <c>ICommitFailable</c>, so what a failure means is decided per operation.
/// </summary>
[DebuggerDisplay("{Value}")]
public union CommitFailure(ConcurrencyConflict, UniqueViolation);

/// <summary>What <see cref="IUnitOfWork.CommitAsync"/> reports: it committed, or it failed in one of the expected ways.</summary>
[DebuggerDisplay("{Value}")]
public union CommitResult(Committed, ConcurrencyConflict, UniqueViolation)
{
    /// <summary>The failure this result reports, if any.</summary>
    /// <value><see langword="null"/> when the commit succeeded; otherwise the matching <see cref="CommitFailure"/>.</value>
#pragma warning disable S3060 // A union's cases are its subclasses; classifying them here is the point.
    public CommitFailure? Failure => this switch
    {
        Committed => null,
        ConcurrencyConflict conflict => conflict,
        UniqueViolation violation => violation,
    };
#pragma warning restore S3060
}
