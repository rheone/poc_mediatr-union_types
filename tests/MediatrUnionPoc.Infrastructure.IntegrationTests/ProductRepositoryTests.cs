using MediatrUnionPoc.Domain;
using MediatrUnionPoc.Infrastructure.IntegrationTests.TestData;
using Microsoft.EntityFrameworkCore;

namespace MediatrUnionPoc.Infrastructure.IntegrationTests;

/// <summary>
/// Exercises <see cref="ProductRepository"/> directly against the real EF Core SQLite provider,
/// covering the query logic <see cref="EfCoreUnitOfWorkTests"/> doesn't: paging's ordering,
/// skip/take math, change-tracking behavior of each read path, and a plain lookup miss. Nothing here substitutes <see cref="AppDbContext"/> —
/// that's the point of an integration test for a repository.
/// </summary>
[Trait("Category", "Integration")]
public class ProductRepositoryTests
{
    /// <summary>
    /// Rows for <see cref="GetPagedAsync_PageRequest_ReturnsSliceAndFullTotal_Test"/> against five
    /// products named A to E. Partitions: first page, a later full page, a final partial page, and a page
    /// past the end (empty slice, total unchanged).
    /// </summary>
#pragma warning disable SA1310 // Field name follows the {TestMethodName}_Data convention
    public static readonly TheoryData<
        int,
        int,
        string[]
    > GetPagedAsync_PageRequest_ReturnsSliceAndFullTotal_Test_Data = new()
    {
        { 1, 2, ["A", "B"] },
        { 2, 2, ["C", "D"] },
        { 3, 2, ["E"] },
        { 4, 2, [] },
    };
#pragma warning restore SA1310

    private const int FirstPage = 1;
    private const int DefaultPageSize = 10;
    private const int SeededProductCount = 5;

    /// <summary>Verifies <see cref="ProductRepository.GetByIdAsync"/> returns <see langword="null"/> for an id that was never added.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull_Test()
    {
        // Arrange
        await using var database = await SqliteDatabaseMother.CreateAsync(
            TestContext.Current.CancellationToken
        );
        await using var dbContext = database.CreateContext();
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
    public async Task GetByIdAsync_AddedAndSavedProduct_ReturnsProduct_Test()
    {
        // Arrange
        await using var database = await SqliteDatabaseMother.CreateAsync(
            TestContext.Current.CancellationToken
        );
        await using var dbContext = database.CreateContext();
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

    /// <summary>Verifies <see cref="ProductRepository.GetByIdAsync"/> rehydrates every stored property in a fresh context.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    // Auto Generated, verify expected behavior: every stored property survives a round trip through the value converters.
    [Fact]
    public async Task GetByIdAsync_PersistedProduct_ReturnsAllStoredProperties_Test()
    {
        // Arrange
        await using var database = await SqliteDatabaseMother.CreateAsync(
            TestContext.Current.CancellationToken
        );
        var product = ProductMother.Widget();
        await database.SeedAsync([product], TestContext.Current.CancellationToken);
        await using var dbContext = database.CreateContext();
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
    public async Task GetByIdAsync_PersistedProduct_ReturnsTrackedEntity_Test()
    {
        // Arrange
        await using var database = await SqliteDatabaseMother.CreateAsync(
            TestContext.Current.CancellationToken
        );
        var product = ProductMother.Widget();
        await database.SeedAsync([product], TestContext.Current.CancellationToken);
        await using var dbContext = database.CreateContext();
        var repository = new ProductRepository(dbContext);

        // Act
        var result = await repository.GetByIdAsync(product.Id, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(EntityState.Unchanged, dbContext.Entry(result).State);
    }

    /// <summary>Verifies <see cref="ProductRepository.AddAsync"/> stages the product without persisting it.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    // Auto Generated, verify expected behavior: AddAsync only stages; the database changes on save.
    [Fact]
    public async Task AddAsync_Product_StagesWithoutPersisting_Test()
    {
        // Arrange
        await using var database = await SqliteDatabaseMother.CreateAsync(
            TestContext.Current.CancellationToken
        );
        await using var dbContext = database.CreateContext();
        var repository = new ProductRepository(dbContext);
        var product = ProductMother.Widget();

        // Act
        await repository.AddAsync(product, CancellationToken.None);

        // Assert
        await using var verifyContext = database.CreateContext();
        Assert.Multiple(
            () => Assert.Equal(EntityState.Added, dbContext.Entry(product).State),
            () => Assert.Empty(verifyContext.Products)
        );
    }

    /// <summary>Verifies <see cref="ProductRepository.AddAsync"/> rejects a <see langword="null"/> product.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    // Auto Generated, verify expected behavior: the repository rejects a null product up front with ArgumentNullException.
    [Fact]
    public async Task AddAsync_NullProduct_ThrowsArgumentNullException_Test()
    {
        // Arrange
        await using var database = await SqliteDatabaseMother.CreateAsync(
            TestContext.Current.CancellationToken
        );
        await using var dbContext = database.CreateContext();
        var repository = new ProductRepository(dbContext);

        // Act
        // Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            repository.AddAsync(null!, CancellationToken.None)
        );
        Assert.Equal("product", exception.ParamName);
    }

    /// <summary>Verifies <see cref="ProductRepository.Remove"/> deletes the row once saved.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Remove_SavedProduct_DeletesRowOnceSaved_Test()
    {
        // Arrange
        await using var database = await SqliteDatabaseMother.CreateAsync(
            TestContext.Current.CancellationToken
        );
        await using var dbContext = database.CreateContext();
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

    /// <summary>Verifies <see cref="ProductRepository.Remove"/> leaves the stored row in place until changes are saved.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    // Auto Generated, verify expected behavior: Remove only stages; the row survives until save.
    [Fact]
    public async Task Remove_SavedProduct_KeepsRowUntilSaved_Test()
    {
        // Arrange
        await using var database = await SqliteDatabaseMother.CreateAsync(
            TestContext.Current.CancellationToken
        );
        var product = ProductMother.Widget();
        await database.SeedAsync([product], TestContext.Current.CancellationToken);
        await using var dbContext = database.CreateContext();
        var repository = new ProductRepository(dbContext);
        var tracked = await repository.GetByIdAsync(product.Id, CancellationToken.None);

        // Act
        repository.Remove(tracked!);

        // Assert
        await using var verifyContext = database.CreateContext();
        Assert.Single(verifyContext.Products);
    }

    /// <summary>Verifies <see cref="ProductRepository.Remove"/> rejects a <see langword="null"/> product.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    // Auto Generated, verify expected behavior: the repository rejects a null product up front with ArgumentNullException.
    [Fact]
    public async Task Remove_NullProduct_ThrowsArgumentNullException_Test()
    {
        // Arrange
        await using var database = await SqliteDatabaseMother.CreateAsync(
            TestContext.Current.CancellationToken
        );
        await using var dbContext = database.CreateContext();
        var repository = new ProductRepository(dbContext);

        // Act
        var act = () => repository.Remove(null!);

        // Assert
        var exception = Assert.Throws<ArgumentNullException>(act);
        Assert.Equal("product", exception.ParamName);
    }

    /// <summary>Verifies <see cref="ProductRepository.GetPagedAsync"/> orders results by name, ascending.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetPagedAsync_UnorderedProducts_OrdersResultsByName_Test()
    {
        // Arrange
        await using var database = await SqliteDatabaseMother.CreateAsync(
            TestContext.Current.CancellationToken
        );
        await using var dbContext = database.CreateContext();
        var repository = new ProductRepository(dbContext);
        await repository.AddAsync(ProductMother.Named("Widget"), CancellationToken.None);
        await repository.AddAsync(ProductMother.Named("Anvil"), CancellationToken.None);
        await repository.AddAsync(ProductMother.Named("Gadget"), CancellationToken.None);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        // Act
        var page = await repository.GetPagedAsync(
            FirstPage,
            DefaultPageSize,
            CancellationToken.None
        );

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
    [MemberData(nameof(GetPagedAsync_PageRequest_ReturnsSliceAndFullTotal_Test_Data))]
    public async Task GetPagedAsync_PageRequest_ReturnsSliceAndFullTotal_Test(
        int pageNumber,
        int pageSize,
        string[] expectedNames
    )
    {
        // Arrange
        await using var database = await SqliteDatabaseMother.CreateAsync(
            TestContext.Current.CancellationToken
        );
        await database.SeedAsync(
            [.. new[] { "A", "B", "C", "D", "E" }.Select(ProductMother.Named)],
            TestContext.Current.CancellationToken
        );
        await using var dbContext = database.CreateContext();
        var repository = new ProductRepository(dbContext);

        // Act
        var page = await repository.GetPagedAsync(pageNumber, pageSize, CancellationToken.None);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(expectedNames, page.Items.Select(p => p.Name)),
            () => Assert.Equal(pageNumber, page.PageNumber),
            () => Assert.Equal(pageSize, page.PageSize),
            () => Assert.Equal(SeededProductCount, page.TotalCount)
        );
    }

    /// <summary>Verifies <see cref="ProductRepository.GetPagedAsync"/> returns an empty page and zero total for an empty table.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    // Auto Generated, verify expected behavior: an empty table yields an empty page, not a failure.
    [Fact]
    public async Task GetPagedAsync_NoProducts_ReturnsEmptyPage_Test()
    {
        // Arrange
        await using var database = await SqliteDatabaseMother.CreateAsync(
            TestContext.Current.CancellationToken
        );
        await using var dbContext = database.CreateContext();
        var repository = new ProductRepository(dbContext);

        // Act
        var page = await repository.GetPagedAsync(
            FirstPage,
            DefaultPageSize,
            CancellationToken.None
        );

        // Assert
        Assert.Multiple(() => Assert.Empty(page.Items), () => Assert.Equal(0, page.TotalCount));
    }

    /// <summary>Verifies <see cref="ProductRepository.GetPagedAsync"/> returns untracked entities (read-only path).</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetPagedAsync_PersistedProduct_ReturnsUntrackedEntities_Test()
    {
        // Arrange
        await using var database = await SqliteDatabaseMother.CreateAsync(
            TestContext.Current.CancellationToken
        );
        await database.SeedAsync([ProductMother.Widget()], TestContext.Current.CancellationToken);
        await using var dbContext = database.CreateContext();
        var repository = new ProductRepository(dbContext);

        // Act
        var page = await repository.GetPagedAsync(
            FirstPage,
            DefaultPageSize,
            CancellationToken.None
        );

        // Assert
        Assert.Multiple(
            () => Assert.Single(page.Items),
            () => Assert.Empty(dbContext.ChangeTracker.Entries<Product>())
        );
    }

    // GetPagedAsync deliberately does not validate pageNumber or pageSize: a value below 1 or a
    // negative size would reach EF Core's Skip/Take, so callers must validate first. That validation
    // lives in the Application layer (GetPagedProductsValidator).

    /// <summary>Verifies <see cref="ProductRepository"/>'s constructor rejects a <see langword="null"/> context.</summary>
    // Auto Generated, verify expected behavior: the guard runs at construction, not on first use.
    [Fact]
    public void Ctor_NullDbContext_ThrowsArgumentNullException_Test()
    {
        // Arrange
        AppDbContext? dbContext = null;

        // Act
        var act = () => new ProductRepository(dbContext!);

        // Assert
        var exception = Assert.Throws<ArgumentNullException>(act);
        Assert.Equal("dbContext", exception.ParamName);
    }
}
