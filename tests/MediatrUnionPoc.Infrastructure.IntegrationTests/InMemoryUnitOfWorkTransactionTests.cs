using MediatrUnionPoc.Infrastructure.IntegrationTests.TestData;
using Microsoft.EntityFrameworkCore;

namespace MediatrUnionPoc.Infrastructure.IntegrationTests;

/// <summary>
/// Exercises the real-transaction branches of <see cref="InMemoryUnitOfWork"/> — the ones
/// unreachable under the EF Core InMemory provider, where <c>BeginTransactionAsync</c> always
/// throws and is swallowed. A test-only SQLite <c>:memory:</c> connection hosts the production
/// <see cref="AppDbContext"/> model so a genuine <c>IDbContextTransaction</c> is created, held,
/// committed, rolled back and disposed.
/// </summary>
[Trait("Category", "Integration")]
public sealed class InMemoryUnitOfWorkTransactionTests : IAsyncLifetime
{
    private SqliteDatabaseMother? _database;

    private SqliteDatabaseMother Database =>
        _database ?? throw new InvalidOperationException("InitializeAsync has not run.");

    /// <inheritdoc/>
    public async ValueTask InitializeAsync() =>
        _database = await SqliteDatabaseMother.CreateAsync();

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_database is not null)
        {
            await _database.DisposeAsync();
        }
    }

    /// <summary>Verifies a relational provider yields a held transaction after <see cref="InMemoryUnitOfWork.BeginTransactionAsync"/>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    // Auto Generated, verify expected behavior: BeginTransactionAsync stores the provider's transaction.
    [Fact]
    public async Task BeginTransactionAsync_RelationalProvider_StartsTransaction_Test()
    {
        // Arrange
        await using var dbContext = Database.CreateContext();
        await using var unitOfWork = new InMemoryUnitOfWork(dbContext);

        // Act
        await unitOfWork.BeginTransactionAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(dbContext.Database.CurrentTransaction);
    }

    /// <summary>Verifies a second begin disposes the first transaction and starts a fresh one.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    // Auto Generated, verify expected behavior: a repeated BeginTransactionAsync cannot leak the earlier transaction.
    [Fact]
    public async Task BeginTransactionAsync_CalledTwice_ReplacesFirstTransaction_Test()
    {
        // Arrange
        await using var dbContext = Database.CreateContext();
        await using var unitOfWork = new InMemoryUnitOfWork(dbContext);
        await unitOfWork.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var first = dbContext.Database.CurrentTransaction;

        // Act
        await unitOfWork.BeginTransactionAsync(TestContext.Current.CancellationToken);

        // Assert
        var second = dbContext.Database.CurrentTransaction;
        Assert.NotNull(second);
        Assert.NotSame(first, second);
    }

    /// <summary>Verifies begin-then-commit persists the staged product and releases the transaction.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    // Auto Generated, verify expected behavior: commit saves, commits and clears the held transaction.
    [Fact]
    public async Task CommitAsync_AfterBeginTransactionAsync_PersistsAndReleasesTransaction_Test()
    {
        // Arrange
        await using var dbContext = Database.CreateContext();
        await using var unitOfWork = new InMemoryUnitOfWork(dbContext);
        var product = ProductMother.Widget();
        await unitOfWork.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await new ProductRepository(dbContext).AddAsync(
            product,
            TestContext.Current.CancellationToken
        );

        // Act
        await unitOfWork.CommitAsync(TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = Database.CreateContext();
        var stored = await verifyContext.Products.SingleOrDefaultAsync(
            p => p.Id == product.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Multiple(
            () => Assert.Null(dbContext.Database.CurrentTransaction),
            () => Assert.NotNull(stored)
        );
    }

    /// <summary>Verifies commit without a prior begin still saves staged changes on a relational provider.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    // Auto Generated, verify expected behavior: CommitAsync does not require a held transaction.
    [Fact]
    public async Task CommitAsync_WithoutBeginTransactionAsync_PersistsStagedChanges_Test()
    {
        // Arrange
        await using var dbContext = Database.CreateContext();
        await using var unitOfWork = new InMemoryUnitOfWork(dbContext);
        var product = ProductMother.Widget();
        await new ProductRepository(dbContext).AddAsync(
            product,
            TestContext.Current.CancellationToken
        );

        // Act
        await unitOfWork.CommitAsync(TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = Database.CreateContext();
        Assert.NotNull(
            await verifyContext.Products.SingleOrDefaultAsync(
                p => p.Id == product.Id,
                TestContext.Current.CancellationToken
            )
        );
    }

    /// <summary>Verifies begin-then-rollback discards even changes already saved inside the transaction.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    // Auto Generated, verify expected behavior: rollback undoes a SaveChanges made within the held transaction.
    [Fact]
    public async Task RollbackAsync_AfterSavedChangesInTransaction_DiscardsChangesAndReleasesTransaction_Test()
    {
        // Arrange
        await using var dbContext = Database.CreateContext();
        await using var unitOfWork = new InMemoryUnitOfWork(dbContext);
        await unitOfWork.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await new ProductRepository(dbContext).AddAsync(
            ProductMother.Widget(),
            TestContext.Current.CancellationToken
        );
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await unitOfWork.RollbackAsync(TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = Database.CreateContext();
        var stored = await verifyContext.Products.ToListAsync(
            TestContext.Current.CancellationToken
        );
        Assert.Multiple(
            () => Assert.Null(dbContext.Database.CurrentTransaction),
            () => Assert.Empty(stored)
        );
    }

    /// <summary>Verifies a commit over a connection closed mid-transaction surfaces the provider's failure unswallowed (from the save or the transaction commit, whichever fails first).</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    // Auto Generated, verify expected behavior: only BeginTransactionAsync swallows provider exceptions; CommitAsync lets them propagate.
    [Fact]
    public async Task CommitAsync_ConnectionClosedMidTransaction_ThrowsInvalidOperationException_Test()
    {
        // Arrange
        await using var dbContext = Database.CreateContext();
        await using var unitOfWork = new InMemoryUnitOfWork(dbContext);
        await unitOfWork.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await new ProductRepository(dbContext).AddAsync(
            ProductMother.Widget(),
            TestContext.Current.CancellationToken
        );
        await Database.Connection.CloseAsync();

        // Act
        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            unitOfWork.CommitAsync(TestContext.Current.CancellationToken)
        );
    }

    /// <summary>Verifies disposing asynchronously while a transaction is held rolls it back.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    // Auto Generated, verify expected behavior: DisposeAsync disposes (and thereby rolls back) a leftover transaction.
    [Fact]
    public async Task DisposeAsync_TransactionHeld_RollsBackSavedChanges_Test()
    {
        // Arrange
        await using var dbContext = Database.CreateContext();
        var unitOfWork = new InMemoryUnitOfWork(dbContext);
        await unitOfWork.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await new ProductRepository(dbContext).AddAsync(
            ProductMother.Widget(),
            TestContext.Current.CancellationToken
        );
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await unitOfWork.DisposeAsync();

        // Assert
        await using var verifyContext = Database.CreateContext();
        var stored = await verifyContext.Products.ToListAsync(
            TestContext.Current.CancellationToken
        );
        Assert.Multiple(
            () => Assert.Null(dbContext.Database.CurrentTransaction),
            () => Assert.Empty(stored)
        );
    }

    /// <summary>Verifies disposing synchronously while a transaction is held rolls it back.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    // Auto Generated, verify expected behavior: Dispose disposes (and thereby rolls back) a leftover transaction.
    [Fact]
    public async Task Dispose_TransactionHeld_RollsBackSavedChanges_Test()
    {
        // Arrange
        await using var dbContext = Database.CreateContext();
        var unitOfWork = new InMemoryUnitOfWork(dbContext);
        await unitOfWork.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await new ProductRepository(dbContext).AddAsync(
            ProductMother.Widget(),
            TestContext.Current.CancellationToken
        );
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var exception = Record.Exception(() => unitOfWork.Dispose());

        // Assert
        await using var verifyContext = Database.CreateContext();
        var stored = await verifyContext.Products.ToListAsync(
            TestContext.Current.CancellationToken
        );
        Assert.Multiple(
            () => Assert.Null(exception),
            () => Assert.Null(dbContext.Database.CurrentTransaction),
            () => Assert.Empty(stored)
        );
    }
}
