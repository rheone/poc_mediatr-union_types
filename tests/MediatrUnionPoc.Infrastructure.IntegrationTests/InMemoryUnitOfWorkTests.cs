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
    public async Task CommitAsync_after_adding_a_product_persists_it_for_a_fresh_context()
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
        Assert.NotNull(await verifyContext.Products.SingleOrDefaultAsync(p => p.Id == product.Id));
    }

    /// <summary>Verifies a rolled-back unit of work's changes never reach the database.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task RollbackAsync_after_adding_a_product_discards_it()
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
        Assert.Null(await verifyContext.Products.SingleOrDefaultAsync(p => p.Id == product.Id));
    }

    /// <summary>Verifies a rollback leaves nothing in the change tracker, so a later commit cannot persist it.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task RollbackAsync_then_CommitAsync_does_not_persist_the_rolled_back_product()
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
        Assert.Empty(await verifyContext.Products.ToListAsync());
    }

    /// <summary>Verifies a rollback detaches every tracked entity, including unmodified ones loaded from the database.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task RollbackAsync_with_a_tracked_unmodified_product_detaches_it()
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
    public async Task RollbackAsync_after_updating_a_tracked_product_keeps_the_stored_values()
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
        var stored = await verifyContext.Products.SingleAsync(p => p.Id == product.Id);
        Assert.Equal(product.Name, stored.Name);
    }

    // Auto Generated, verify expected behavior: the update path of CommitAsync.
    /// <summary>Verifies committing an in-place update to a tracked product persists the new values.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task CommitAsync_after_updating_a_tracked_product_persists_the_update()
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
        var stored = await verifyContext.Products.SingleAsync(p => p.Id == product.Id);
        Assert.Multiple(
            () => Assert.Equal(UpdatedName, stored.Name),
            () => Assert.Equal(UpdatedPrice, stored.Price.Value)
        );
    }

    // Auto Generated, verify expected behavior: CommitAsync does not require BeginTransactionAsync first.
    /// <summary>Verifies committing without a prior <see cref="InMemoryUnitOfWork.BeginTransactionAsync"/> still saves staged changes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task CommitAsync_without_BeginTransactionAsync_persists_staged_changes()
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
        Assert.NotNull(await verifyContext.Products.SingleOrDefaultAsync(p => p.Id == product.Id));
    }

    // Auto Generated, verify expected behavior: the token reaches SaveChangesAsync.
    /// <summary>Verifies a cancelled token stops <see cref="InMemoryUnitOfWork.CommitAsync"/> before anything is saved.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task CommitAsync_with_a_cancelled_token_throws_and_persists_nothing()
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
        var act = () => unitOfWork.CommitAsync(cancellation.Token);

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(act);
        await using var verifyContext = DbContextMother.Create(databaseName);
        Assert.Empty(await verifyContext.Products.ToListAsync());
    }

    // Auto Generated, verify expected behavior: the swallowed provider exception documented on BeginTransactionAsync.
    /// <summary>Verifies the InMemory provider's lack of transaction support is swallowed rather than surfaced.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task BeginTransactionAsync_on_a_provider_without_transactions_completes_without_throwing()
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

    // Auto Generated, verify expected behavior: dispose with nothing to clean up is a no-op.
    /// <summary>Verifies <see cref="InMemoryUnitOfWork.DisposeAsync"/> is safe when no transaction is held.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task DisposeAsync_with_no_transaction_completes_without_throwing()
    {
        // Arrange
        await using var dbContext = DbContextMother.Create(DatabaseName());
        var unitOfWork = new InMemoryUnitOfWork(dbContext);

        // Act
        var exception = await Record.ExceptionAsync(() => unitOfWork.DisposeAsync().AsTask());

        // Assert
        Assert.Null(exception);
    }

    // Auto Generated, verify expected behavior: dispose with nothing to clean up is a no-op.
    /// <summary>Verifies <see cref="InMemoryUnitOfWork.Dispose"/> is safe when no transaction is held.</summary>
    [Fact]
    public void Dispose_with_no_transaction_completes_without_throwing()
    {
        // Arrange
        using var dbContext = DbContextMother.Create(DatabaseName());
        var unitOfWork = new InMemoryUnitOfWork(dbContext);

        // Act
        var exception = Record.Exception(unitOfWork.Dispose);

        // Assert
        Assert.Null(exception);
    }

    // SWEEP-AMBIGUITY: BeginTransactionAsync's "dispose a held transaction first" branch and every
    // `_transaction is not null` branch in Commit/Rollback/Dispose are unreachable under the InMemory
    // provider (BeginTransaction always throws and is swallowed, so _transaction stays null). The code
    // handles a real transaction; no test here can exercise that without a relational provider.
}
