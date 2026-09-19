using System.Runtime.CompilerServices;
using MediatrUnionPoc.Domain;
using MediatrUnionPoc.Infrastructure.IntegrationTests.TestData;
using Microsoft.EntityFrameworkCore;

namespace MediatrUnionPoc.Infrastructure.IntegrationTests;

/// <summary>
/// Exercises <see cref="EfCoreUnitOfWork"/> end-to-end against the EF Core InMemory provider (no
/// transactions) and a SQLite <c>:memory:</c> connection (real transactions), confirming that a
/// rolled-back unit of work never reaches the database while a committed one does — the behavior
/// <see cref="MediatrUnionPoc.Application.Common.Behaviors.TransactionBehavior{TRequest,TResponse}"/>
/// depends on. Commit, rollback and dispose semantics are theories asserted identically on both
/// providers; only what a provider does about holding a transaction is asserted per provider.
/// </summary>
[Trait("Category", "Integration")]
public class EfCoreUnitOfWorkTests
{
    private const string UpdatedName = "Changed";
    private const decimal UpdatedPrice = 1m;

    /// <summary>Gets every provider the shared contract is asserted against.</summary>
    public static TheoryData<UnitOfWorkProvider> Providers =>
        new(UnitOfWorkProvider.InMemory, UnitOfWorkProvider.Sqlite);

    private static Task<UnitOfWorkTestDatabase> CreateDatabaseAsync(
        UnitOfWorkProvider provider,
        [CallerMemberName] string test = ""
    ) =>
        UnitOfWorkTestDatabase.CreateAsync(
            provider,
            DbContextMother.NameFor(nameof(EfCoreUnitOfWorkTests), $"{test}.{provider}")
        );

    /// <summary>Verifies a committed unit of work's changes are visible to a fresh context.</summary>
    /// <param name="provider">The provider under test.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task CommitAsync_AddedProduct_PersistsForFreshContext_Test(
        UnitOfWorkProvider provider
    )
    {
        // Arrange
        await using var database = await CreateDatabaseAsync(provider);
        await using var dbContext = database.CreateContext();
        var repository = new ProductRepository(dbContext);
        await using var unitOfWork = new EfCoreUnitOfWork(dbContext);
        var product = ProductMother.Widget();
        await unitOfWork.BeginTransactionAsync(CancellationToken.None);
        await repository.AddAsync(product, CancellationToken.None);

        // Act
        await unitOfWork.CommitAsync(CancellationToken.None);

        // Assert
        await using var verifyContext = database.CreateContext();
        Assert.NotNull(
            await verifyContext.Products.SingleOrDefaultAsync(
                p => p.Id == product.Id,
                TestContext.Current.CancellationToken
            )
        );
    }

    /// <summary>Verifies a rolled-back unit of work's changes never reach the database.</summary>
    /// <param name="provider">The provider under test.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task RollbackAsync_AddedProduct_DiscardsProduct_Test(UnitOfWorkProvider provider)
    {
        // Arrange
        await using var database = await CreateDatabaseAsync(provider);
        await using var dbContext = database.CreateContext();
        var repository = new ProductRepository(dbContext);
        await using var unitOfWork = new EfCoreUnitOfWork(dbContext);
        var product = ProductMother.Widget();
        await unitOfWork.BeginTransactionAsync(CancellationToken.None);
        await repository.AddAsync(product, CancellationToken.None);

        // Act
        await unitOfWork.RollbackAsync(CancellationToken.None);

        // Assert
        await using var verifyContext = database.CreateContext();
        Assert.Null(
            await verifyContext.Products.SingleOrDefaultAsync(
                p => p.Id == product.Id,
                TestContext.Current.CancellationToken
            )
        );
    }

    /// <summary>Verifies a rollback leaves nothing in the change tracker, so a later commit cannot persist it.</summary>
    /// <param name="provider">The provider under test.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task RollbackAsync_FollowedByCommitAsync_DoesNotPersistRolledBackProduct_Test(
        UnitOfWorkProvider provider
    )
    {
        // Arrange
        await using var database = await CreateDatabaseAsync(provider);
        await using var dbContext = database.CreateContext();
        var repository = new ProductRepository(dbContext);
        await using var unitOfWork = new EfCoreUnitOfWork(dbContext);
        await unitOfWork.BeginTransactionAsync(CancellationToken.None);
        await repository.AddAsync(ProductMother.Widget(), CancellationToken.None);

        // Act
        await unitOfWork.RollbackAsync(CancellationToken.None);
        await unitOfWork.CommitAsync(CancellationToken.None);

        // Assert
        await using var verifyContext = database.CreateContext();
        Assert.Empty(
            await verifyContext.Products.ToListAsync(TestContext.Current.CancellationToken)
        );
    }

    /// <summary>Verifies a rollback detaches every tracked entity, including unmodified ones loaded from the database.</summary>
    /// <param name="provider">The provider under test.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task RollbackAsync_TrackedUnmodifiedProduct_DetachesProduct_Test(
        UnitOfWorkProvider provider
    )
    {
        // Arrange
        await using var database = await CreateDatabaseAsync(provider);
        var product = ProductMother.Widget();
        await database.SeedAsync(product);
        await using var dbContext = database.CreateContext();
        await new ProductRepository(dbContext).GetByIdAsync(product.Id, CancellationToken.None);
        await using var unitOfWork = new EfCoreUnitOfWork(dbContext);

        // Act
        await unitOfWork.RollbackAsync(CancellationToken.None);

        // Assert
        Assert.Empty(dbContext.ChangeTracker.Entries<Product>());
    }

    /// <summary>Verifies a rollback discards an in-place update to a tracked product.</summary>
    /// <param name="provider">The provider under test.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task RollbackAsync_UpdatedTrackedProduct_KeepsStoredValues_Test(
        UnitOfWorkProvider provider
    )
    {
        // Arrange
        await using var database = await CreateDatabaseAsync(provider);
        var product = ProductMother.Widget();
        await database.SeedAsync(product);
        await using var dbContext = database.CreateContext();
        var tracked = await new ProductRepository(dbContext).GetByIdAsync(
            product.Id,
            CancellationToken.None
        );
        await using var unitOfWork = new EfCoreUnitOfWork(dbContext);
        tracked!.UpdateDetails(UpdatedName, Money.From(UpdatedPrice));

        // Act
        await unitOfWork.RollbackAsync(CancellationToken.None);
        await unitOfWork.CommitAsync(CancellationToken.None);

        // Assert
        await using var verifyContext = database.CreateContext();
        var stored = await verifyContext.Products.SingleAsync(
            p => p.Id == product.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(product.Name, stored.Name);
    }

    /// <summary>Verifies committing an in-place update to a tracked product persists the new values.</summary>
    /// <param name="provider">The provider under test.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task CommitAsync_UpdatedTrackedProduct_PersistsUpdate_Test(
        UnitOfWorkProvider provider
    )
    {
        // Arrange
        await using var database = await CreateDatabaseAsync(provider);
        var product = ProductMother.Widget();
        await database.SeedAsync(product);
        await using var dbContext = database.CreateContext();
        var tracked = await new ProductRepository(dbContext).GetByIdAsync(
            product.Id,
            CancellationToken.None
        );
        await using var unitOfWork = new EfCoreUnitOfWork(dbContext);
        await unitOfWork.BeginTransactionAsync(CancellationToken.None);
        tracked!.UpdateDetails(UpdatedName, Money.From(UpdatedPrice));

        // Act
        await unitOfWork.CommitAsync(CancellationToken.None);

        // Assert
        await using var verifyContext = database.CreateContext();
        var stored = await verifyContext.Products.SingleAsync(
            p => p.Id == product.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Multiple(
            () => Assert.Equal(UpdatedName, stored.Name),
            () => Assert.Equal(UpdatedPrice, stored.Price.Value)
        );
    }

    /// <summary>Verifies committing without a prior <see cref="EfCoreUnitOfWork.BeginTransactionAsync"/> still saves staged changes.</summary>
    /// <param name="provider">The provider under test.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task CommitAsync_WithoutBeginTransactionAsync_PersistsStagedChanges_Test(
        UnitOfWorkProvider provider
    )
    {
        // Arrange
        await using var database = await CreateDatabaseAsync(provider);
        await using var dbContext = database.CreateContext();
        var repository = new ProductRepository(dbContext);
        await using var unitOfWork = new EfCoreUnitOfWork(dbContext);
        var product = ProductMother.Widget();
        await repository.AddAsync(product, CancellationToken.None);

        // Act
        await unitOfWork.CommitAsync(CancellationToken.None);

        // Assert
        await using var verifyContext = database.CreateContext();
        Assert.NotNull(
            await verifyContext.Products.SingleOrDefaultAsync(
                p => p.Id == product.Id,
                TestContext.Current.CancellationToken
            )
        );
    }

    /// <summary>Verifies a cancelled token stops <see cref="EfCoreUnitOfWork.CommitAsync"/> before anything is saved.</summary>
    /// <param name="provider">The provider under test.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task CommitAsync_CancelledToken_ThrowsAndPersistsNothing_Test(
        UnitOfWorkProvider provider
    )
    {
        // Arrange
        await using var database = await CreateDatabaseAsync(provider);
        await using var dbContext = database.CreateContext();
        var repository = new ProductRepository(dbContext);
        await using var unitOfWork = new EfCoreUnitOfWork(dbContext);
        await repository.AddAsync(ProductMother.Widget(), CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            unitOfWork.CommitAsync(cancellation.Token)
        );
        await using var verifyContext = database.CreateContext();
        Assert.Empty(
            await verifyContext.Products.ToListAsync(TestContext.Current.CancellationToken)
        );
    }

    /// <summary>Verifies <see cref="EfCoreUnitOfWork.DisposeAsync"/> is safe when no transaction is held.</summary>
    /// <param name="provider">The provider under test.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task DisposeAsync_NoTransaction_CompletesWithoutThrowing_Test(
        UnitOfWorkProvider provider
    )
    {
        // Arrange
        await using var database = await CreateDatabaseAsync(provider);
        await using var dbContext = database.CreateContext();
        var unitOfWork = new EfCoreUnitOfWork(dbContext);

        // Act
        var exception = await Record.ExceptionAsync(() => unitOfWork.DisposeAsync().AsTask());

        // Assert
        Assert.Null(exception);
    }

    /// <summary>Verifies <see cref="EfCoreUnitOfWork.Dispose"/> is safe when no transaction is held.</summary>
    /// <param name="provider">The provider under test.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Dispose_NoTransaction_CompletesWithoutThrowing_Test(
        UnitOfWorkProvider provider
    )
    {
        // Arrange
        await using var database = await CreateDatabaseAsync(provider);
        await using var dbContext = database.CreateContext();
        var unitOfWork = new EfCoreUnitOfWork(dbContext);

        // Act
        var exception = Record.Exception(() => unitOfWork.Dispose());

        // Assert
        Assert.Null(exception);
    }

    /// <summary>Verifies a provider without transaction support begins without throwing and holds no transaction.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task BeginTransactionAsync_ProviderWithoutTransactions_HoldsNoTransaction_Test()
    {
        // Arrange
        await using var database = await CreateDatabaseAsync(UnitOfWorkProvider.InMemory);
        await using var dbContext = database.CreateContext();
        await using var unitOfWork = new EfCoreUnitOfWork(dbContext);

        // Act
        var exception = await Record.ExceptionAsync(() =>
            unitOfWork.BeginTransactionAsync(CancellationToken.None)
        );

        // Assert
        Assert.Multiple(
            () => Assert.Null(exception),
            () => Assert.Null(dbContext.Database.CurrentTransaction)
        );
    }

    /// <summary>Verifies a relational provider yields a held transaction after <see cref="EfCoreUnitOfWork.BeginTransactionAsync"/>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task BeginTransactionAsync_RelationalProvider_StartsTransaction_Test()
    {
        // Arrange
        await using var database = await CreateDatabaseAsync(UnitOfWorkProvider.Sqlite);
        await using var dbContext = database.CreateContext();
        await using var unitOfWork = new EfCoreUnitOfWork(dbContext);

        // Act
        await unitOfWork.BeginTransactionAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(dbContext.Database.CurrentTransaction);
    }

    /// <summary>Verifies a second begin disposes the first transaction and starts a fresh one.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task BeginTransactionAsync_CalledTwice_ReplacesFirstTransaction_Test()
    {
        // Arrange
        await using var database = await CreateDatabaseAsync(UnitOfWorkProvider.Sqlite);
        await using var dbContext = database.CreateContext();
        await using var unitOfWork = new EfCoreUnitOfWork(dbContext);
        await unitOfWork.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var first = dbContext.Database.CurrentTransaction;

        // Act
        await unitOfWork.BeginTransactionAsync(TestContext.Current.CancellationToken);

        // Assert
        var second = dbContext.Database.CurrentTransaction;
        Assert.NotNull(second);
        Assert.NotSame(first, second);
    }

    /// <summary>Verifies a failure to start a transaction on a relational provider propagates instead of being swallowed.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task BeginTransactionAsync_RelationalProviderFailsToBegin_Throws_Test()
    {
        // Arrange
        await using var database = await CreateDatabaseAsync(UnitOfWorkProvider.Sqlite);
        await using var dbContext = database.CreateContext();
        await using var unitOfWork = new EfCoreUnitOfWork(dbContext);
        await using var foreignTransaction = await dbContext.Database.BeginTransactionAsync(
            TestContext.Current.CancellationToken
        );

        // Act
        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            unitOfWork.BeginTransactionAsync(TestContext.Current.CancellationToken)
        );
    }

    /// <summary>Verifies begin-then-commit persists the staged product and releases the transaction.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task CommitAsync_AfterBeginTransactionAsync_PersistsAndReleasesTransaction_Test()
    {
        // Arrange
        await using var database = await CreateDatabaseAsync(UnitOfWorkProvider.Sqlite);
        await using var dbContext = database.CreateContext();
        await using var unitOfWork = new EfCoreUnitOfWork(dbContext);
        var product = ProductMother.Widget();
        await unitOfWork.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await new ProductRepository(dbContext).AddAsync(
            product,
            TestContext.Current.CancellationToken
        );

        // Act
        await unitOfWork.CommitAsync(TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = database.CreateContext();
        var stored = await verifyContext.Products.SingleOrDefaultAsync(
            p => p.Id == product.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Multiple(
            () => Assert.Null(dbContext.Database.CurrentTransaction),
            () => Assert.NotNull(stored)
        );
    }

    /// <summary>Verifies begin-then-rollback discards even changes already saved inside the transaction.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task RollbackAsync_AfterSavedChangesInTransaction_DiscardsChangesAndReleasesTransaction_Test()
    {
        // Arrange
        await using var database = await CreateDatabaseAsync(UnitOfWorkProvider.Sqlite);
        await using var dbContext = database.CreateContext();
        await using var unitOfWork = new EfCoreUnitOfWork(dbContext);
        await unitOfWork.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await new ProductRepository(dbContext).AddAsync(
            ProductMother.Widget(),
            TestContext.Current.CancellationToken
        );
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await unitOfWork.RollbackAsync(TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = database.CreateContext();
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
    [Fact]
    public async Task CommitAsync_ConnectionClosedMidTransaction_ThrowsInvalidOperationException_Test()
    {
        // Arrange
        await using var database = await CreateDatabaseAsync(UnitOfWorkProvider.Sqlite);
        await using var dbContext = database.CreateContext();
        await using var unitOfWork = new EfCoreUnitOfWork(dbContext);
        await unitOfWork.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await new ProductRepository(dbContext).AddAsync(
            ProductMother.Widget(),
            TestContext.Current.CancellationToken
        );
        await database.Sqlite.Connection.CloseAsync();

        // Act
        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            unitOfWork.CommitAsync(TestContext.Current.CancellationToken)
        );
    }

    /// <summary>Verifies disposing asynchronously while a transaction is held rolls it back.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task DisposeAsync_TransactionHeld_RollsBackSavedChanges_Test()
    {
        // Arrange
        await using var database = await CreateDatabaseAsync(UnitOfWorkProvider.Sqlite);
        await using var dbContext = database.CreateContext();
        var unitOfWork = new EfCoreUnitOfWork(dbContext);
        await unitOfWork.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await new ProductRepository(dbContext).AddAsync(
            ProductMother.Widget(),
            TestContext.Current.CancellationToken
        );
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await unitOfWork.DisposeAsync();

        // Assert
        await using var verifyContext = database.CreateContext();
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
    [Fact]
    public async Task Dispose_TransactionHeld_RollsBackSavedChanges_Test()
    {
        // Arrange
        await using var database = await CreateDatabaseAsync(UnitOfWorkProvider.Sqlite);
        await using var dbContext = database.CreateContext();
        var unitOfWork = new EfCoreUnitOfWork(dbContext);
        await unitOfWork.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await new ProductRepository(dbContext).AddAsync(
            ProductMother.Widget(),
            TestContext.Current.CancellationToken
        );
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var exception = Record.Exception(() => unitOfWork.Dispose());

        // Assert
        await using var verifyContext = database.CreateContext();
        var stored = await verifyContext.Products.ToListAsync(
            TestContext.Current.CancellationToken
        );
        Assert.Multiple(
            () => Assert.Null(exception),
            () => Assert.Null(dbContext.Database.CurrentTransaction),
            () => Assert.Empty(stored)
        );
    }

    /// <summary>Verifies <see cref="EfCoreUnitOfWork"/>'s constructor rejects a <see langword="null"/> context.</summary>
    [Fact]
    public void Ctor_NullDbContext_ThrowsArgumentNullException_Test()
    {
        // Arrange
        AppDbContext? dbContext = null;

        // Act
        var act = () => new EfCoreUnitOfWork(dbContext!);

        // Assert
        var exception = Assert.Throws<ArgumentNullException>(act);
        Assert.Equal("dbContext", exception.ParamName);
    }
}
