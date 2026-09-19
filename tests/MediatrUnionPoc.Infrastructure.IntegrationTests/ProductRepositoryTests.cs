using System.Runtime.CompilerServices;
using MediatrUnionPoc.Domain;
using MediatrUnionPoc.Infrastructure.IntegrationTests.TestData;
using Microsoft.EntityFrameworkCore;

namespace MediatrUnionPoc.Infrastructure.IntegrationTests;

/// <summary>
/// Exercises <see cref="ProductRepository"/> directly against the real EF Core InMemory provider,
/// covering the query logic <see cref="InMemoryUnitOfWorkTests"/> doesn't: paging's ordering,
/// skip/take math, change-tracking behavior of each read path, and a plain lookup miss. Nothing here substitutes <see cref="AppDbContext"/> —
/// that's the point of an integration test for a repository.
/// </summary>
[Trait("Category", "Integration")]
public class ProductRepositoryTests
{
    /// <summary>
    /// Rows for <see cref="GetPagedAsync_given_a_page_returns_that_slice_and_the_full_total"/> against five
    /// products named A to E. Partitions: first page, a later full page, a final partial page, and a page
    /// past the end (empty slice, total unchanged).
    /// </summary>
    public static readonly TheoryData<
        int,
        int,
        string[]
    > GetPagedAsync_given_a_page_returns_that_slice_and_the_full_total_Data = new()
    {
        { 1, 2, ["A", "B"] },
        { 2, 2, ["C", "D"] },
        { 3, 2, ["E"] },
        { 4, 2, [] },
    };

    private static string DatabaseName([CallerMemberName] string test = "") =>
        DbContextMother.NameFor(nameof(ProductRepositoryTests), test);

    /// <summary>Verifies <see cref="ProductRepository.GetByIdAsync"/> returns <see langword="null"/> for an id that was never added.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetByIdAsync_given_an_id_that_was_never_added_returns_null()
    {
        // Arrange
        await using var dbContext = DbContextMother.Create(DatabaseName());
        var repository = new ProductRepository(dbContext);

        // Act
        var result = await repository.GetByIdAsync(
            ProductMother.UnknownId(),
            CancellationToken.None
        );

        // Assert
        Assert.Null(result);
    }

    /// <summary>Verifies <see cref="ProductRepository.GetByIdAsync"/> returns a product once it's been added and saved.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetByIdAsync_given_an_added_and_saved_product_returns_it()
    {
        // Arrange
        await using var dbContext = DbContextMother.Create(DatabaseName());
        var repository = new ProductRepository(dbContext);
        var product = ProductMother.Widget();
        await repository.AddAsync(product, CancellationToken.None);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        // Act
        var result = await repository.GetByIdAsync(product.Id, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(product.Id, result.Id);
    }

    // Auto Generated, verify expected behavior: every stored property survives a round trip through the value converters.
    /// <summary>Verifies <see cref="ProductRepository.GetByIdAsync"/> rehydrates every stored property in a fresh context.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetByIdAsync_given_a_persisted_product_returns_all_stored_properties()
    {
        // Arrange
        var databaseName = DatabaseName();
        var product = ProductMother.Widget();
        await DbContextMother.SeedAsync(databaseName, product);
        await using var dbContext = DbContextMother.Create(databaseName);
        var repository = new ProductRepository(dbContext);

        // Act
        var result = await repository.GetByIdAsync(product.Id, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Multiple(
            () => Assert.Equal(product.Name, result.Name),
            () => Assert.Equal(product.Price, result.Price),
            () => Assert.Equal(product.OwnerId, result.OwnerId)
        );
    }

    /// <summary>Verifies <see cref="ProductRepository.GetByIdAsync"/> returns a tracked entity, so callers can mutate and save it.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetByIdAsync_given_a_persisted_product_returns_a_tracked_entity()
    {
        // Arrange
        var databaseName = DatabaseName();
        var product = ProductMother.Widget();
        await DbContextMother.SeedAsync(databaseName, product);
        await using var dbContext = DbContextMother.Create(databaseName);
        var repository = new ProductRepository(dbContext);

        // Act
        var result = await repository.GetByIdAsync(product.Id, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(EntityState.Unchanged, dbContext.Entry(result).State);
    }

    // Auto Generated, verify expected behavior: AddAsync only stages; the database changes on save.
    /// <summary>Verifies <see cref="ProductRepository.AddAsync"/> stages the product without persisting it.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task AddAsync_given_a_product_stages_it_without_persisting()
    {
        // Arrange
        var databaseName = DatabaseName();
        await using var dbContext = DbContextMother.Create(databaseName);
        var repository = new ProductRepository(dbContext);
        var product = ProductMother.Widget();

        // Act
        await repository.AddAsync(product, CancellationToken.None);

        // Assert
        await using var verifyContext = DbContextMother.Create(databaseName);
        Assert.Multiple(
            () => Assert.Equal(EntityState.Added, dbContext.Entry(product).State),
            () => Assert.Empty(verifyContext.Products)
        );
    }

    // Auto Generated, verify expected behavior: EF Core rejects a null entity with ArgumentNullException.
    /// <summary>Verifies <see cref="ProductRepository.AddAsync"/> rejects a <see langword="null"/> product.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task AddAsync_given_null_throws_ArgumentNullException()
    {
        // Arrange
        await using var dbContext = DbContextMother.Create(DatabaseName());
        var repository = new ProductRepository(dbContext);

        // Act
        var act = () => repository.AddAsync(null!, CancellationToken.None);

        // Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(act);
        Assert.Equal("entity", exception.ParamName);
    }

    /// <summary>Verifies <see cref="ProductRepository.Remove"/> deletes the row once saved.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Remove_given_a_saved_product_deletes_it_once_saved()
    {
        // Arrange
        await using var dbContext = DbContextMother.Create(DatabaseName());
        var repository = new ProductRepository(dbContext);
        var product = ProductMother.Widget();
        await repository.AddAsync(product, CancellationToken.None);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        // Act
        repository.Remove(product);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        // Assert
        Assert.Null(await repository.GetByIdAsync(product.Id, CancellationToken.None));
    }

    // Auto Generated, verify expected behavior: Remove only stages; the row survives until save.
    /// <summary>Verifies <see cref="ProductRepository.Remove"/> leaves the stored row in place until changes are saved.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Remove_given_a_saved_product_keeps_the_row_until_saved()
    {
        // Arrange
        var databaseName = DatabaseName();
        var product = ProductMother.Widget();
        await DbContextMother.SeedAsync(databaseName, product);
        await using var dbContext = DbContextMother.Create(databaseName);
        var repository = new ProductRepository(dbContext);
        var tracked = await repository.GetByIdAsync(product.Id, CancellationToken.None);

        // Act
        repository.Remove(tracked!);

        // Assert
        await using var verifyContext = DbContextMother.Create(databaseName);
        Assert.Single(verifyContext.Products);
    }

    // Auto Generated, verify expected behavior: EF Core rejects a null entity with ArgumentNullException.
    /// <summary>Verifies <see cref="ProductRepository.Remove"/> rejects a <see langword="null"/> product.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Remove_given_null_throws_ArgumentNullException()
    {
        // Arrange
        await using var dbContext = DbContextMother.Create(DatabaseName());
        var repository = new ProductRepository(dbContext);

        // Act
        var act = () => repository.Remove(null!);

        // Assert
        var exception = Assert.Throws<ArgumentNullException>(act);
        Assert.Equal("entity", exception.ParamName);
    }

    /// <summary>Verifies <see cref="ProductRepository.GetPagedAsync"/> orders results by name, ascending.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetPagedAsync_given_unordered_products_orders_results_by_name()
    {
        // Arrange
        await using var dbContext = DbContextMother.Create(DatabaseName());
        var repository = new ProductRepository(dbContext);
        await repository.AddAsync(ProductMother.Named("Widget"), CancellationToken.None);
        await repository.AddAsync(ProductMother.Named("Anvil"), CancellationToken.None);
        await repository.AddAsync(ProductMother.Named("Gadget"), CancellationToken.None);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        // Act
        var page = await repository.GetPagedAsync(1, 10, CancellationToken.None);

        // Assert
        Assert.Collection(
            page.Items,
            p => Assert.Equal("Anvil", p.Name),
            p => Assert.Equal("Gadget", p.Name),
            p => Assert.Equal("Widget", p.Name)
        );
    }

    /// <summary>
    /// Verifies <see cref="ProductRepository.GetPagedAsync"/> skips and takes the right rows for each page and
    /// always reports the true total, even past the last page.
    /// </summary>
    /// <param name="pageNumber">The 1-based page requested.</param>
    /// <param name="pageSize">The page size requested.</param>
    /// <param name="expectedNames">The names expected on that page.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [MemberData(nameof(GetPagedAsync_given_a_page_returns_that_slice_and_the_full_total_Data))]
    public async Task GetPagedAsync_given_a_page_returns_that_slice_and_the_full_total(
        int pageNumber,
        int pageSize,
        string[] expectedNames
    )
    {
        // Arrange
        var databaseName = $"{DatabaseName()}.{pageNumber}";
        await DbContextMother.SeedAsync(
            databaseName,
            [.. new[] { "A", "B", "C", "D", "E" }.Select(ProductMother.Named)]
        );
        await using var dbContext = DbContextMother.Create(databaseName);
        var repository = new ProductRepository(dbContext);

        // Act
        var page = await repository.GetPagedAsync(pageNumber, pageSize, CancellationToken.None);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(expectedNames, page.Items.Select(p => p.Name)),
            () => Assert.Equal(pageNumber, page.PageNumber),
            () => Assert.Equal(pageSize, page.PageSize),
            () => Assert.Equal(5, page.TotalCount)
        );
    }

    // Auto Generated, verify expected behavior: an empty table yields an empty page, not a failure.
    /// <summary>Verifies <see cref="ProductRepository.GetPagedAsync"/> returns an empty page and zero total for an empty table.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetPagedAsync_given_no_products_returns_an_empty_page()
    {
        // Arrange
        await using var dbContext = DbContextMother.Create(DatabaseName());
        var repository = new ProductRepository(dbContext);

        // Act
        var page = await repository.GetPagedAsync(1, 10, CancellationToken.None);

        // Assert
        Assert.Multiple(() => Assert.Empty(page.Items), () => Assert.Equal(0, page.TotalCount));
    }

    /// <summary>Verifies <see cref="ProductRepository.GetPagedAsync"/> returns untracked entities (read-only path).</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetPagedAsync_given_a_persisted_product_returns_untracked_entities()
    {
        // Arrange
        var databaseName = DatabaseName();
        await DbContextMother.SeedAsync(databaseName, ProductMother.Widget());
        await using var dbContext = DbContextMother.Create(databaseName);
        var repository = new ProductRepository(dbContext);

        // Act
        var page = await repository.GetPagedAsync(1, 10, CancellationToken.None);

        // Assert
        Assert.Single(page.Items);
        Assert.Empty(dbContext.ChangeTracker.Entries<Product>());
    }

    // SWEEP-AMBIGUITY: ProductRepository's constructor takes no null check on AppDbContext (primary
    // constructor), so `new ProductRepository(null!)` succeeds and fails later with a
    // NullReferenceException on first use. It should arguably throw ArgumentNullException up front;
    // no test asserts either behavior. Likewise GetPagedAsync accepts pageNumber < 1 or pageSize < 0
    // with no validation (a negative Skip/Take reaches EF Core); validation lives in the Application
    // layer, so nothing is pinned here.
}
