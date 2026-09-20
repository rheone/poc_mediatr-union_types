namespace MediatrUnionPoc.Domain;

/// <summary>
/// Coordinates a single logical transaction across the repositories injected into a handler.
/// Repository methods only stage changes; nothing reaches the database until
/// <see cref="CommitAsync"/> runs, and <see cref="RollbackAsync"/> discards whatever is staged.
/// Intended usage is exactly one begin/commit-or-rollback cycle per command — driven from the
/// Application layer's transaction pipeline behavior, which is the only place this interface is
/// used from in this codebase.
/// </summary>
/// <remarks>
/// There is one implementation per persistence technology, named for it (e.g. the EF Core one);
/// differences between providers of the same technology are the implementation's concern, handled
/// inside it, and never leak into this contract.
/// </remarks>
public interface IUnitOfWork
{
    /// <summary>
    /// Starts a new logical transaction. Where the underlying store has no transaction support,
    /// this is a safe no-op rather than a fault; a genuine failure to start one is not swallowed.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation; defaults to <see cref="CancellationToken.None"/>.</param>
    /// <returns>A task that completes once the transaction has started (or the no-op has been recorded).</returns>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists every change staged since <see cref="BeginTransactionAsync"/> and commits. An
    /// expected refusal (a stale write, a uniqueness violation) is reported in the returned
    /// <see cref="CommitResult"/> rather than thrown; nothing is persisted in that case, and the
    /// caller is expected to roll back.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation; defaults to <see cref="CancellationToken.None"/>.</param>
    /// <returns>A task yielding <see cref="Committed"/>, or the failure that stopped the commit.</returns>
    Task<CommitResult> CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>Discards every change staged since <see cref="BeginTransactionAsync"/> without persisting any of it.</summary>
    /// <param name="cancellationToken">Token to cancel the operation; defaults to <see cref="CancellationToken.None"/>.</param>
    /// <returns>A task that completes once the rollback finishes.</returns>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
