using MediatrUnionPoc.Domain;
using Microsoft.Data.Sqlite;
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
/// The <see cref="IUnitOfWork"/> adapter for EF Core, named for the persistence technology it
/// adapts rather than for a provider. It assumes a relational provider: every begin starts a real
/// database transaction, and a failure to start one propagates. A different persistence technology
/// (NHibernate, say) would be a separate <see cref="IUnitOfWork"/> implementation, not a branch
/// inside this one.
/// </remarks>
/// <exception cref="ArgumentNullException"><paramref name="dbContext"/> is <see langword="null"/>.</exception>
public sealed class EfCoreUnitOfWork(AppDbContext dbContext)
    : IUnitOfWork,
        IDisposable,
        IAsyncDisposable
{
    /// <summary>SQLite's <c>SQLITE_CONSTRAINT_UNIQUE</c> extended result code.</summary>
    private const int SqliteUniqueConstraintExtendedCode = 2067;

    private readonly AppDbContext _dbContext =
        dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    private IDbContextTransaction? _transaction;

    /// <inheritdoc/>
    /// <remarks>
    /// Starts a real database transaction; any failure to start one propagates. Disposes any
    /// transaction already held first, so a repeated call cannot leak it.
    /// </remarks>
    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }

        _transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A <see cref="DbUpdateConcurrencyException"/> — a row's concurrency token no longer matched
    /// the stored one, or the row was deleted meanwhile — is reported as
    /// <see cref="ConcurrencyConflict"/> instead of propagating. The transaction is left open; the
    /// caller rolls it back, which also detaches the stale entities. A unique-constraint failure on
    /// the product-name index (SQLite extended error 2067, <c>UNIQUE constraint failed:
    /// Products.NormalizedName</c>) is reported as <see cref="UniqueViolation"/> likewise. Every other
    /// exception — including a primary-key collision (extended error 1555) — is unexpected and
    /// propagates. Telling the index apart relies on the SQLite message naming the column, the only
    /// identifier SQLite reports; a provider that reports the index name instead would need its own
    /// check here.
    /// </remarks>
    public async Task<CommitResult> CommitAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ConcurrencyConflict();
        }
        catch (DbUpdateException ex) when (IsProductNameUniqueViolation(ex))
        {
            return new UniqueViolation();
        }

        if (_transaction is not null)
        {
            await _transaction.CommitAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }

        return new Committed();
    }

    private static bool IsProductNameUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqliteException sqlite
        && sqlite.SqliteExtendedErrorCode == SqliteUniqueConstraintExtendedCode
        && sqlite.Message.Contains(
            $"{nameof(AppDbContext.Products)}.{nameof(Product.NormalizedName)}",
            StringComparison.Ordinal
        );

    /// <inheritdoc/>
    /// <remarks>
    /// Rolls back the held transaction (if any) and detaches every tracked entity. Without
    /// detaching, staged-but-unsaved changes would stay in the change tracker and could be
    /// persisted by a later <see cref="CommitAsync"/> on the same scoped context.
    /// </remarks>
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }

        foreach (var entry in _dbContext.ChangeTracker.Entries().ToList())
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
