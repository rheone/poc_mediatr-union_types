using MediatrUnionPoc.Domain;
using MediatrUnionPoc.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MediatrUnionPoc.Infrastructure.IntegrationTests;

/// <summary>
/// Exercises the real EF Core InMemory provider (via <see cref="ProductIdValueConverter"/> and
/// <see cref="MoneyValueConverter"/>) end-to-end, confirming that a rolled-back unit of work
/// never reaches the database while a committed one does — the behavior
/// <see cref="MediatrUnionPoc.Application.Common.Behaviors.TransactionBehavior{TRequest,TResponse}"/>
/// depends on.
/// </summary>
public class InMemoryUnitOfWorkTests
{
    /// <summary>Creates an <see cref="AppDbContext"/> backed by a named EF Core InMemory database.</summary>
    /// <param name="databaseName">The InMemory database name; reused across contexts to share state.</param>
    /// <returns>A new <see cref="AppDbContext"/> instance pointed at the named database.</returns>
    private static AppDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>Verifies a committed unit of work's changes are visible to a fresh context.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Commit_persists_pending_changes()
    {
        var databaseName = Guid.NewGuid().ToString();
        await using var dbContext = CreateContext(databaseName);
        var repository = new ProductRepository(dbContext);
        await using var unitOfWork = new InMemoryUnitOfWork(dbContext);

        var product = Product.Create("Widget", Money.From(9.99m));
        await unitOfWork.BeginTransactionAsync(CancellationToken.None);
        await repository.AddAsync(product, CancellationToken.None);
        await unitOfWork.CommitAsync(CancellationToken.None);

        await using var verifyContext = CreateContext(databaseName);
        Assert.NotNull(await verifyContext.Products.SingleOrDefaultAsync(p => p.Id == product.Id));
    }

    /// <summary>Verifies a rolled-back unit of work's changes never reach the database.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Rollback_discards_pending_changes()
    {
        var databaseName = Guid.NewGuid().ToString();
        await using var dbContext = CreateContext(databaseName);
        var repository = new ProductRepository(dbContext);
        await using var unitOfWork = new InMemoryUnitOfWork(dbContext);

        var product = Product.Create("Widget", Money.From(9.99m));
        await unitOfWork.BeginTransactionAsync(CancellationToken.None);
        await repository.AddAsync(product, CancellationToken.None);
        await unitOfWork.RollbackAsync(CancellationToken.None);

        await using var verifyContext = CreateContext(databaseName);
        Assert.Null(await verifyContext.Products.SingleOrDefaultAsync(p => p.Id == product.Id));
    }
}
