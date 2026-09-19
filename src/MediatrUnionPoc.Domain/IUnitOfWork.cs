namespace MediatrUnionPoc.Domain;

/// <summary>
/// Coordinates a single logical transaction across the repositories injected into a handler.
/// Repository methods only stage changes against a shared tracked context; nothing reaches the
/// database until <see cref="CommitAsync"/> runs. Intended usage is exactly one begin/commit-or-rollback
/// cycle per command — driven from the Application layer's transaction pipeline behavior, which is
/// the only place this interface is used from in this codebase.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Starts tracking a new logical transaction. On a provider that doesn't support real
    /// transactions (e.g. EF Core's InMemory provider), this is a safe no-op rather than a fault.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation; defaults to <see cref="CancellationToken.None"/>.</param>
    /// <returns>A task that completes once the transaction has started (or the no-op has been recorded).</returns>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>Persists every change staged since <see cref="BeginTransactionAsync"/> and commits.</summary>
    /// <param name="cancellationToken">Token to cancel the operation; defaults to <see cref="CancellationToken.None"/>.</param>
    /// <returns>A task that completes once the changes are committed.</returns>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>Discards every change staged since <see cref="BeginTransactionAsync"/> without persisting any of it.</summary>
    /// <param name="cancellationToken">Token to cancel the operation; defaults to <see cref="CancellationToken.None"/>.</param>
    /// <returns>A task that completes once the rollback finishes.</returns>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
