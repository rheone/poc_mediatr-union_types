using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Application.Common.Abstractions;

/// <summary>
/// Lets <see cref="Behaviors.TransactionBehavior{TRequest,TResponse}"/> report a failed commit
/// through the command's own response union, without knowing which case types that union has. A
/// commit can fail in a few expected ways (see <see cref="CommitFailure"/>) and what each one
/// means is operation-specific: a stale write on an update is a precondition failure, the same
/// failure for a brand-new row is impossible and so an error. Because
/// <see cref="FromCommitFailure"/> must be a <c>switch</c> over <see cref="CommitFailure"/>, the
/// compiler forces every transactional union to classify every commit failure — including one added
/// later.
/// </summary>
/// <typeparam name="TSelf">The implementing union type itself.</typeparam>
public interface ICommitFailable<TSelf>
    where TSelf : ICommitFailable<TSelf>
{
    /// <summary>Builds this union's response for a commit that failed.</summary>
    /// <param name="failure">How the commit failed.</param>
    /// <returns>The union value reporting that failure in this operation's own terms.</returns>
    static abstract TSelf FromCommitFailure(CommitFailure failure);
}
