using System.Runtime.CompilerServices;
using MediatrUnionPoc.Domain;
using MediatrUnionPoc.Infrastructure.IntegrationTests.TestData;
using Microsoft.EntityFrameworkCore;

namespace MediatrUnionPoc.Infrastructure.IntegrationTests;

/// <summary>
/// Exercises the real EF Core InMemory provider (via <see cref="ProductIdValueConverter"/> and
/// <see cref="MoneyValueConverter"/>) end-to-end, confirming that a rolled-back unit of work
/// never reaches the database while a committed one does — the behavior
/// <see cref="MediatrUnionPoc.Application.Common.Behaviors.TransactionBehavior{TRequest,TResponse}"/>
/// depends on.
/// </summary>
[Trait("Category", "Integration")]
public class InMemoryUnitOfWorkTests
{
    private const string UpdatedName = "Changed";
    private const decimal UpdatedPrice = 1m;

    private static string DatabaseName([CallerMemberName] string test = "") =>
        DbContextMother.NameFor(nameof(InMemoryUnitOfWorkTests), test);

    /// <summary>Verifies a committed unit of work's changes are visible to a fresh context.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task CommitAsync_AddedProduct_PersistsForFreshContext_Test()
    {
        // Arrange
        var databaseName = DatabaseName();
        await using var dbContext = DbContextMother.Create(databaseName);
        var repository = new ProductRepository(dbContext);
        await using var unitOfWork = new InMemoryUnitOfWork(dbContext);
        var product = ProductMother.Widget();
        await unitOfWork.BeginTransactionAsync(CancellationToken.None);
        await repository.AddAsync(product, CancellationToken.None);

        // Act
        await unitOfWork.CommitAsync(CancellationToken.None);

        // Assert
        await using var verifyContext = DbContextMother.Create(databaseName);
        Assert.NotNull(
            await verifyContext.Products.SingleOrDefaultAsync(
                p => p.Id == product.Id,
                TestContext.Current.CancellationToken
            )
        );
    }

    /// <summary>Verifies a rolled-back unit of work's changes never reach the database.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task RollbackAsync_AddedProduct_DiscardsProduct_Test()
    {
        // Arrange
        var databaseName = DatabaseName();
        await using var dbContext = DbContextMother.Create(databaseName);
        var repository = new ProductRepository(dbContext);
        await using var unitOfWork = new InMemoryUnitOfWork(dbContext);
        var product = ProductMother.Widget();
        await unitOfWork.BeginTransactionAsync(CancellationToken.None);
        await repository.AddAsync(product, CancellationToken.None);

        // Act
        await unitOfWork.RollbackAsync(CancellationToken.None);

        // Assert
        await using var verifyContext = DbContextMother.Create(databaseName);
        Assert.Null(
            await verifyContext.Products.SingleOrDefaultAsync(
                p => p.Id == product.Id,
                TestContext.Current.CancellationToken
            )
        );
    }

    /// <summary>Verifies a rollback leaves nothing in the change tracker, so a later commit cannot persist it.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task RollbackAsync_FollowedByCommitAsync_DoesNotPersistRolledBackProduct_Test()
    {
        // Arrange
        var databaseName = DatabaseName();
        await using var dbContext = DbContextMother.Create(databaseName);
        var repository = new ProductRepository(dbContext);
        await using var unitOfWork = new InMemoryUnitOfWork(dbContext);
        await unitOfWork.BeginTransactionAsync(CancellationToken.None);
        await repository.AddAsync(ProductMother.Widget(), CancellationToken.None);

        // Act
        await unitOfWork.RollbackAsync(CancellationToken.None);
        await unitOfWork.CommitAsync(CancellationToken.None);

        // Assert
        await using var verifyContext = DbContextMother.Create(databaseName);
        Assert.Empty(
            await verifyContext.Products.ToListAsync(TestContext.Current.CancellationToken)
        );
    }

    /// <summary>Verifies a rollback detaches every tracked entity, including unmodified ones loaded from the database.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task RollbackAsync_TrackedUnmodifiedProduct_DetachesProduct_Test()
    {
        // Arrange
        var databaseName = DatabaseName();
        var product = ProductMother.Widget();
        await DbContextMother.SeedAsync(databaseName, product);
        await using var dbContext = DbContextMother.Create(databaseName);
        await new ProductRepository(dbContext).GetByIdAsync(product.Id, CancellationToken.None);
        await using var unitOfWork = new InMemoryUnitOfWork(dbContext);

        // Act
        await unitOfWork.RollbackAsync(CancellationToken.None);

        // Assert
        Assert.Empty(dbContext.ChangeTracker.Entries<Product>());
    }

    /// <summary>Verifies a rollback discards an in-place update to a tracked product.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task RollbackAsync_UpdatedTrackedProduct_KeepsStoredValues_Test()
    {
        // Arrange
        var databaseName = DatabaseName();
        var product = ProductMother.Widget();
        await DbContextMother.SeedAsync(databaseName, product);
        await using var dbContext = DbContextMother.Create(databaseName);
        var tracked = await new ProductRepository(dbContext).GetByIdAsync(
            product.Id,
            CancellationToken.None
        );
        await using var unitOfWork = new InMemoryUnitOfWork(dbContext);
        tracked!.UpdateDetails(UpdatedName, Money.From(UpdatedPrice));

        // Act
        await unitOfWork.RollbackAsync(CancellationToken.None);
        await unitOfWork.CommitAsync(CancellationToken.None);

        // Assert
        await using var verifyContext = DbContextMother.Create(databaseName);
        var stored = await verifyContext.Products.SingleAsync(
            p => p.Id == product.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(product.Name, stored.Name);
    }

    /// <summary>Verifies committing an in-place update to a tracked product persists the new values.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    // Auto Generated, verify expected behavior: the update path of CommitAsync.
    [Fact]
    public async Task CommitAsync_UpdatedTrackedProduct_PersistsUpdate_Test()
    {
        // Arrange
        var databaseName = DatabaseName();
        var product = ProductMother.Widget();
        await DbContextMother.SeedAsync(databaseName, product);
        await using var dbContext = DbContextMother.Create(databaseName);
        var tracked = await new ProductRepository(dbContext).GetByIdAsync(
            product.Id,
            CancellationToken.None
        );
        await using var unitOfWork = new InMemoryUnitOfWork(dbContext);
        await unitOfWork.BeginTransactionAsync(CancellationToken.None);
        tracked!.UpdateDetails(UpdatedName, Money.From(UpdatedPrice));

        // Act
        await unitOfWork.CommitAsync(CancellationToken.None);

        // Assert
        await using var verifyContext = DbContextMother.Create(databaseName);
        var stored = await verifyContext.Products.SingleAsync(
            p => p.Id == product.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Multiple(
            () => Assert.Equal(UpdatedName, stored.Name),
            () => Assert.Equal(UpdatedPrice, stored.Price.Value)
        );
    }

    /// <summary>Verifies committing without a prior <see cref="InMemoryUnitOfWork.BeginTransactionAsync"/> still saves staged changes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    // Auto Generated, verify expected behavior: CommitAsync does not require BeginTransactionAsync first.
    [Fact]
    public async Task CommitAsync_WithoutBeginTransactionAsync_PersistsStagedChanges_Test()
    {
        // Arrange
        var databaseName = DatabaseName();
        await using var dbContext = DbContextMother.Create(databaseName);
        var repository = new ProductRepository(dbContext);
        await using var unitOfWork = new InMemoryUnitOfWork(dbContext);
        var product = ProductMother.Widget();
        await repository.AddAsync(product, CancellationToken.None);

        // Act
        await unitOfWork.CommitAsync(CancellationToken.None);

        // Assert
        await using var verifyContext = DbContextMother.Create(databaseName);
        Assert.NotNull(
            await verifyContext.Products.SingleOrDefaultAsync(
                p => p.Id == product.Id,
                TestContext.Current.CancellationToken
            )
        );
    }

    /// <summary>Verifies a cancelled token stops <see cref="InMemoryUnitOfWork.CommitAsync"/> before anything is saved.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    // Auto Generated, verify expected behavior: the token reaches SaveChangesAsync.
    [Fact]
    public async Task CommitAsync_CancelledToken_ThrowsAndPersistsNothing_Test()
    {
        // Arrange
        var databaseName = DatabaseName();
        await using var dbContext = DbContextMother.Create(databaseName);
        var repository = new ProductRepository(dbContext);
        await using var unitOfWork = new InMemoryUnitOfWork(dbContext);
        await repository.AddAsync(ProductMother.Widget(), CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            unitOfWork.CommitAsync(cancellation.Token)
        );
        await using var verifyContext = DbContextMother.Create(databaseName);
        Assert.Empty(
            await verifyContext.Products.ToListAsync(TestContext.Current.CancellationToken)
        );
    }

    /// <summary>Verifies the InMemory provider's lack of transaction support is swallowed rather than surfaced.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    // Auto Generated, verify expected behavior: the swallowed provider exception documented on BeginTransactionAsync.
    [Fact]
    public async Task BeginTransactionAsync_ProviderWithoutTransactions_CompletesWithoutThrowing_Test()
    {
        // Arrange
        await using var dbContext = DbContextMother.Create(DatabaseName());
        await using var unitOfWork = new InMemoryUnitOfWork(dbContext);

        // Act
        var exception = await Record.ExceptionAsync(() =>
            unitOfWork.BeginTransactionAsync(CancellationToken.None)
        );

        // Assert
        Assert.Null(exception);
    }

    /// <summary>Verifies <see cref="InMemoryUnitOfWork.DisposeAsync"/> is safe when no transaction is held.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    // Auto Generated, verify expected behavior: dispose with nothing to clean up is a no-op.
    [Fact]
    public async Task DisposeAsync_NoTransaction_CompletesWithoutThrowing_Test()
    {
        // Arrange
        await using var dbContext = DbContextMother.Create(DatabaseName());
        var unitOfWork = new InMemoryUnitOfWork(dbContext);

        // Act
        var exception = await Record.ExceptionAsync(() => unitOfWork.DisposeAsync().AsTask());

        // Assert
        Assert.Null(exception);
    }

    /// <summary>Verifies <see cref="InMemoryUnitOfWork.Dispose"/> is safe when no transaction is held.</summary>
    // Auto Generated, verify expected behavior: dispose with nothing to clean up is a no-op.
    [Fact]
    public void Dispose_NoTransaction_CompletesWithoutThrowing_Test()
    {
        // Arrange
        using var dbContext = DbContextMother.Create(DatabaseName());
        var unitOfWork = new InMemoryUnitOfWork(dbContext);

        // Act
        var exception = Record.Exception(() => unitOfWork.Dispose());

        // Assert
        Assert.Null(exception);
    }

    /// <summary>Verifies <see cref="InMemoryUnitOfWork"/>'s constructor rejects a <see langword="null"/> context.</summary>
    // Auto Generated, verify expected behavior: the guard runs at construction, not on first use.
    [Fact]
    public void Ctor_NullDbContext_ThrowsArgumentNullException_Test()
    {
        // Arrange
        AppDbContext? dbContext = null;

        // Act
        var act = () => new InMemoryUnitOfWork(dbContext!);

        // Assert
        var exception = Assert.Throws<ArgumentNullException>(act);
        Assert.Equal("dbContext", exception.ParamName);
    }

    // The `_transaction is not null` branches (begin-twice, commit, rollback, dispose) cannot run under
    // the InMemory provider; InMemoryUnitOfWorkTransactionTests covers them against SQLite.
}
