using MediatrUnionPoc.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MediatrUnionPoc.Infrastructure;

/// <summary>
/// Coordinates commit/rollback for whatever repositories were injected into the current handler,
/// all of which share the same scoped <see cref="AppDbContext"/>. Repositories only mutate the
/// change tracker; <see cref="Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync(CancellationToken)"/>
/// is only ever called from <see cref="CommitAsync"/>, so an error-case result never reaches the database.
/// </summary>
/// <remarks>
/// Named for the provider it's written against, not just "the" <see cref="IUnitOfWork"/>
/// implementation: this class encodes two behaviors specific to EF Core's InMemory provider — a
/// swallowed <see cref="BeginTransactionAsync"/> failure, and a manual change-tracker detach on
/// rollback — that a relational-provider adapter would neither need nor want to replicate. If this
/// POC ever grows a second, relational adapter, that's a second, differently-named class, not a
/// branch inside this one.
/// </remarks>
public sealed class InMemoryUnitOfWork(AppDbContext dbContext)
    : IUnitOfWork,
        IDisposable,
        IAsyncDisposable
{
    private IDbContextTransaction? _transaction;

    /// <inheritdoc/>
    /// <remarks>
    /// The InMemory provider backing this POC doesn't support relational transactions and throws
    /// on <c>DatabaseFacade.BeginTransactionAsync</c> depending on the EF Core version; that failure is swallowed here and treated as "no
    /// transaction to manage" rather than propagated, since it's an expected provider limitation,
    /// not an error condition. A real relational provider would get a genuine transaction instead.
    /// Disposes any transaction already held before starting a new one — <see cref="IUnitOfWork"/>
    /// is documented for exactly one begin/commit-or-rollback cycle per scope, but a second,
    /// unexpected call to this method should never silently leak the first transaction.
    /// </remarks>
    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }

        try
        {
            _transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException)
        {
            _transaction = null;
        }
    }

    /// <inheritdoc/>
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);

        if (_transaction is not null)
        {
            await _transaction.CommitAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Detaches every tracked entity in addition to rolling back the relational transaction (if
    /// any), because <see cref="Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync(CancellationToken)"/>
    /// was never called for this unit of work — without detaching, a staged-but-unsaved change would still be sitting in the change
    /// tracker and could be persisted accidentally by a later, unrelated call to
    /// <see cref="CommitAsync"/> on the same scoped context.
    /// </remarks>
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }

        foreach (var entry in dbContext.ChangeTracker.Entries().ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    /// <summary>
    /// Disposes a transaction left over from a scope that never called <see cref="CommitAsync"/>
    /// or <see cref="RollbackAsync"/> — normally unreachable in practice, since
    /// <see cref="MediatrUnionPoc.Application.Common.Behaviors.TransactionBehavior{TRequest,TResponse}"/>
    /// always calls one or the other, but a real cleanup path is cheaper than relying on that
    /// always being true.
    /// </summary>
    /// <returns>A task that completes once any leftover transaction is disposed.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    /// <summary>
    /// Synchronous counterpart to <see cref="DisposeAsync"/> — the built-in DI container disposes
    /// a scope synchronously unless every scoped service in it is <see cref="IAsyncDisposable"/>
    /// only, so this type needs both to work as a normal <c>Scoped</c> registration.
    /// </summary>
    public void Dispose()
    {
        _transaction?.Dispose();
        _transaction = null;
    }
}
