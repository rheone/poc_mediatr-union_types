using MediatrUnionPoc.Domain;
using Microsoft.EntityFrameworkCore;

namespace MediatrUnionPoc.Infrastructure.IntegrationTests;

/// <summary>
/// Exercises <see cref="ProductRepository"/> directly against the real EF Core InMemory provider,
/// covering the query logic <see cref="InMemoryUnitOfWorkTests"/> doesn't: paging's ordering,
/// skip/take math, and a plain lookup miss. Nothing here substitutes <see cref="AppDbContext"/> —
/// that's the point of an integration test for a repository.
/// </summary>
public class ProductRepositoryTests
{
    private static AppDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>Verifies <see cref="ProductRepository.GetByIdAsync"/> returns <see langword="null"/> for an id that was never added.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetByIdAsync_returns_null_for_a_product_that_was_never_added()
    {
        await using var dbContext = CreateContext(Guid.NewGuid().ToString());
        var repository = new ProductRepository(dbContext);

        var result = await repository.GetByIdAsync(ProductId.New(), CancellationToken.None);

        Assert.Null(result);
    }

    /// <summary>Verifies <see cref="ProductRepository.GetByIdAsync"/> returns a product once it's been added and saved.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetByIdAsync_returns_the_product_once_added_and_saved()
    {
        await using var dbContext = CreateContext(Guid.NewGuid().ToString());
        var repository = new ProductRepository(dbContext);
        var product = Product.Create("Widget", Money.From(9.99m));
        await repository.AddAsync(product, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var result = await repository.GetByIdAsync(product.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(product.Id, result.Id);
    }

    /// <summary>Verifies <see cref="ProductRepository.Remove"/> deletes the row once saved.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Remove_deletes_the_product_once_saved()
    {
        await using var dbContext = CreateContext(Guid.NewGuid().ToString());
        var repository = new ProductRepository(dbContext);
        var product = Product.Create("Widget", Money.From(9.99m));
        await repository.AddAsync(product, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        repository.Remove(product);
        await dbContext.SaveChangesAsync();

        Assert.Null(await repository.GetByIdAsync(product.Id, CancellationToken.None));
    }

    /// <summary>Verifies <see cref="ProductRepository.GetPagedAsync"/> orders results by name, ascending.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetPagedAsync_orders_results_by_name()
    {
        await using var dbContext = CreateContext(Guid.NewGuid().ToString());
        var repository = new ProductRepository(dbContext);
        await repository.AddAsync(
            Product.Create("Widget", Money.From(9.99m)),
            CancellationToken.None
        );
        await repository.AddAsync(
            Product.Create("Anvil", Money.From(19.99m)),
            CancellationToken.None
        );
        await repository.AddAsync(
            Product.Create("Gadget", Money.From(4.99m)),
            CancellationToken.None
        );
        await dbContext.SaveChangesAsync();

        var page = await repository.GetPagedAsync(1, 10, CancellationToken.None);

        Assert.Collection(
            page.Items,
            p => Assert.Equal("Anvil", p.Name),
            p => Assert.Equal("Gadget", p.Name),
            p => Assert.Equal("Widget", p.Name)
        );
    }

    /// <summary>Verifies <see cref="ProductRepository.GetPagedAsync"/> skips and takes the correct rows for a page past the first.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetPagedAsync_skips_and_takes_the_correct_rows_for_a_later_page()
    {
        await using var dbContext = CreateContext(Guid.NewGuid().ToString());
        var repository = new ProductRepository(dbContext);
        foreach (var name in new[] { "A", "B", "C", "D", "E" })
        {
            await repository.AddAsync(Product.Create(name, Money.From(1m)), CancellationToken.None);
        }

        await dbContext.SaveChangesAsync();

        var page = await repository.GetPagedAsync(
            pageNumber: 2,
            pageSize: 2,
            CancellationToken.None
        );

        Assert.Collection(
            page.Items,
            p => Assert.Equal("C", p.Name),
            p => Assert.Equal("D", p.Name)
        );
        Assert.Equal(2, page.PageNumber);
        Assert.Equal(2, page.PageSize);
        Assert.Equal(5, page.TotalCount);
    }

    /// <summary>Verifies <see cref="ProductRepository.GetPagedAsync"/> reports the true total count even when it exceeds the page size.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetPagedAsync_reports_the_total_count_across_all_pages()
    {
        await using var dbContext = CreateContext(Guid.NewGuid().ToString());
        var repository = new ProductRepository(dbContext);
        foreach (var name in new[] { "A", "B", "C" })
        {
            await repository.AddAsync(Product.Create(name, Money.From(1m)), CancellationToken.None);
        }

        await dbContext.SaveChangesAsync();

        var page = await repository.GetPagedAsync(
            pageNumber: 1,
            pageSize: 2,
            CancellationToken.None
        );

        Assert.Equal(2, page.Items.Count);
        Assert.Equal(3, page.TotalCount);
    }
}
