namespace MediatrUnionPoc.Application.Common.Abstractions;

/// <summary>
/// Lets <see cref="Behaviors.TransactionBehavior{TRequest,TResponse}"/> decide commit vs. rollback
/// generically, without knowing — or assuming anything about — which case types a command's union
/// declares. Case types themselves are deliberately meaning-free (an arbitrary <c>Error</c>,
/// <c>Success</c>, or a dev's own record can be reused across unions to mean whatever that union's
/// author wants); classification lives on the union instead, one union at a time. Because
/// <see cref="ShouldCommit"/> must be implemented as a <c>switch</c> over the union's own <c>this</c>
/// value, the C# compiler's union-exhaustiveness check forces every one of that union's case types
/// to be classified — including a case type added after this method was first written. There is no
/// default branch to silently fall through: a union with an unhandled case fails to compile.
/// </summary>
/// <typeparam name="TSelf">The implementing union type itself.</typeparam>
public interface ITransactionOutcome<TSelf>
    where TSelf : ITransactionOutcome<TSelf>
{
    /// <summary>Classifies one instance of this union as a commit or a rollback.</summary>
    /// <param name="response">The union value to classify.</param>
    /// <returns><see langword="true"/> if the unit of work should commit; <see langword="false"/> if it should roll back.</returns>
    static abstract bool ShouldCommit(TSelf response);
}
