using MediatrUnionPoc.Domain;
using MediatrUnionPoc.Infrastructure.IntegrationTests.TestData;

namespace MediatrUnionPoc.Infrastructure.IntegrationTests;

/// <summary>
/// Exercises <see cref="ProductRepository.GetPagedAsync"/>'s filtering, sorting and paging
/// against real SQLite: each criterion alone and combined, every sort field in both directions,
/// multi-key sorts, and the implicit <see cref="Product.Id"/> tiebreaker.
/// </summary>
[Trait("Category", "Integration")]
public class ProductRepositoryListingTests
{
    private static readonly IReadOnlyList<ProductSort> NoSort = [];

    /// <summary>
    /// Rows for <see cref="GetPagedAsync_SingleSortKey_OrdersByThatFieldAndDirection_Test"/>. The seed
    /// (see <see cref="SortSeed"/>) is built so name, price and creation order all differ:
    /// names Alpha, Bravo, Charlie; prices 100, 9.5, 20 (numeric order differs from text order); creation instants 11:00Z, 12:00Z, 10:00Z.
    /// </summary>
#pragma warning disable SA1310 // Field name follows the {TestMethodName}_Data convention
    public static readonly TheoryData<
        ProductSortField,
        SortDirection,
        string[]
    > GetPagedAsync_SingleSortKey_OrdersByThatFieldAndDirection_Test_Data = new()
    {
        { ProductSortField.Name, SortDirection.Ascending, ["Alpha", "Bravo", "Charlie"] },
        { ProductSortField.Name, SortDirection.Descending, ["Charlie", "Bravo", "Alpha"] },
        { ProductSortField.Price, SortDirection.Ascending, ["Bravo", "Charlie", "Alpha"] },
        { ProductSortField.Price, SortDirection.Descending, ["Alpha", "Charlie", "Bravo"] },
        { ProductSortField.CreatedAt, SortDirection.Ascending, ["Charlie", "Alpha", "Bravo"] },
        { ProductSortField.CreatedAt, SortDirection.Descending, ["Bravo", "Alpha", "Charlie"] },
    };
#pragma warning restore SA1310

    /// <summary>
    /// Three products whose creation instants are given with offsets chosen so that ordering their
    /// local-time text would reverse the true instant order (Bravo's text sorts first, Charlie's last):
    /// a sort that is right here is sorting instants, not strings.
    /// </summary>
    private static Product[] SortSeed() =>
        [
            ProductMother.With(
                "Alpha",
                100m,
                new DateTimeOffset(2026, 5, 1, 11, 0, 0, TimeSpan.Zero)
            ),
            ProductMother.With(
                "Bravo",
                9.5m,
                new DateTimeOffset(2026, 5, 1, 1, 0, 0, TimeSpan.FromHours(-11))
            ),
            ProductMother.With(
                "Charlie",
                20m,
                new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.FromHours(2))
            ),
        ];

    private static async Task<PagedResult<Product>> ListAsync(
        IReadOnlyCollection<Product> seed,
        ProductCriteria criteria,
        IReadOnlyList<ProductSort> sort,
        int pageNumber = 1,
        int pageSize = 50
    )
    {
        await using var database = await SqliteDatabaseMother.CreateAsync(
            TestContext.Current.CancellationToken
        );
        await database.SeedAsync(seed, TestContext.Current.CancellationToken);
        await using var dbContext = database.CreateContext();
        var repository = new ProductRepository(dbContext);

        return await repository.GetPagedAsync(
            pageNumber,
            pageSize,
            criteria,
            sort,
            TestContext.Current.CancellationToken
        );
    }

    /// <summary>Verifies the name filter matches a substring case-insensitively, ignores surrounding whitespace in the search text, and counts only matches.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetPagedAsync_NameContains_KeepsCaseInsensitiveTrimmedSubstringMatches_Test()
    {
        // Arrange
        Product[] seed =
        [
            ProductMother.With("Blue Widget"),
            ProductMother.With("Red WIDGET Pro"),
            ProductMother.With("Anvil"),
        ];

        // Act
        var page = await ListAsync(seed, new ProductCriteria(NameContains: "  widget "), NoSort);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(["Blue Widget", "Red WIDGET Pro"], page.Items.Select(p => p.Name)),
            () => Assert.Equal(2, page.TotalCount)
        );
    }

    /// <summary>Verifies the minimum price is inclusive and compares numerically (9.99 is below 10, 100 is above 20).</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetPagedAsync_MinPrice_KeepsProductsAtOrAboveItNumerically_Test()
    {
        // Arrange
        Product[] seed =
        [
            ProductMother.With("A", 9.99m),
            ProductMother.With("B", 10m),
            ProductMother.With("C", 20m),
            ProductMother.With("D", 100m),
        ];

        // Act
        var page = await ListAsync(seed, new ProductCriteria(MinPrice: 10m), NoSort);

        // Assert
        Assert.Equal(["B", "C", "D"], page.Items.Select(p => p.Name));
    }

    /// <summary>Verifies the maximum price is inclusive and compares numerically.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetPagedAsync_MaxPrice_KeepsProductsAtOrBelowItNumerically_Test()
    {
        // Arrange
        Product[] seed =
        [
            ProductMother.With("A", 9.99m),
            ProductMother.With("B", 20m),
            ProductMother.With("C", 20.01m),
            ProductMother.With("D", 100m),
        ];

        // Act
        var page = await ListAsync(seed, new ProductCriteria(MaxPrice: 20m), NoSort);

        // Assert
        Assert.Equal(["A", "B"], page.Items.Select(p => p.Name));
    }

    /// <summary>Verifies the owner filter matches the owner id exactly.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetPagedAsync_OwnerId_KeepsOnlyThatOwnersProducts_Test()
    {
        // Arrange
        Product[] seed =
        [
            ProductMother.With("Mine", ownerId: "alice"),
            ProductMother.With("Theirs", ownerId: "bob"),
            ProductMother.With("Also Mine", ownerId: "alice"),
            ProductMother.With("Shouty", ownerId: "ALICE"),
        ];

        // Act
        var page = await ListAsync(seed, new ProductCriteria(OwnerId: "alice"), NoSort);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(["Also Mine", "Mine"], page.Items.Select(p => p.Name)),
            () => Assert.Equal(2, page.TotalCount)
        );
    }

    /// <summary>Verifies every criterion combines with AND, and the total counts only products matching all of them.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetPagedAsync_AllCriteria_AreCombinedWithAnd_Test()
    {
        // Arrange
        Product[] seed =
        [
            ProductMother.With("Blue Widget", 15m, ownerId: "alice"),
            ProductMother.With("Blue Widget Deluxe", 50m, ownerId: "alice"),
            ProductMother.With("Blue Widget Mini", 12m, ownerId: "bob"),
            ProductMother.With("Green Gadget", 15m, ownerId: "alice"),
            ProductMother.With("Blue Widget Cheap", 5m, ownerId: "alice"),
        ];
        var criteria = new ProductCriteria("widget", 10m, 20m, "alice");

        // Act
        var page = await ListAsync(seed, criteria, NoSort);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(["Blue Widget"], page.Items.Select(p => p.Name)),
            () => Assert.Equal(1, page.TotalCount)
        );
    }

    /// <summary>Verifies each allowlisted field sorts ascending and descending; creation time orders by instant, not by text.</summary>
    /// <param name="field">The field to sort by.</param>
    /// <param name="direction">The direction to sort in.</param>
    /// <param name="expectedNames">The names in the expected order.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [MemberData(nameof(GetPagedAsync_SingleSortKey_OrdersByThatFieldAndDirection_Test_Data))]
    public async Task GetPagedAsync_SingleSortKey_OrdersByThatFieldAndDirection_Test(
        ProductSortField field,
        SortDirection direction,
        string[] expectedNames
    )
    {
        // Arrange
        var sort = new[] { new ProductSort(field, direction) };

        // Act
        var page = await ListAsync(SortSeed(), ProductCriteria.None, sort);

        // Assert
        Assert.Equal(expectedNames, page.Items.Select(p => p.Name));
    }

    /// <summary>Verifies later sort keys break ties left by earlier ones, each in its own direction.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetPagedAsync_MultipleSortKeys_LaterKeysBreakTiesInTheirOwnDirection_Test()
    {
        // Arrange
        Product[] seed =
        [
            ProductMother.With("A", 10m),
            ProductMother.With("B", 20m),
            ProductMother.With("C", 10m),
            ProductMother.With("D", 20m),
        ];
        ProductSort[] sort =
        [
            new(ProductSortField.Price, SortDirection.Descending),
            new(ProductSortField.Name, SortDirection.Ascending),
        ];

        // Act
        var page = await ListAsync(seed, ProductCriteria.None, sort);

        // Assert
        Assert.Equal(["B", "D", "A", "C"], page.Items.Select(p => p.Name));
    }

    /// <summary>Verifies rows tying on every requested key come back in the same order however they were inserted, because the Id tiebreaker makes the order total.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetPagedAsync_TiedRows_OrderDoesNotDependOnInsertionOrder_Test()
    {
        // Arrange
        Product[] products =
        [
            .. Enumerable.Range(1, 6).Select(i => ProductMother.With($"P{i}", 5m)),
        ];
        ProductSort[] sort = [new(ProductSortField.Price, SortDirection.Ascending)];

        // Act
        var forward = await ListAsync(products, ProductCriteria.None, sort);
        var backward = await ListAsync([.. products.Reverse()], ProductCriteria.None, sort);

        // Assert
        Assert.Equal(forward.Items.Select(p => p.Id), backward.Items.Select(p => p.Id));
    }

    /// <summary>Verifies walking every page of a fully tied sort visits each product exactly once.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetPagedAsync_TiedRowsAcrossPages_VisitsEveryProductExactlyOnce_Test()
    {
        // Arrange
        await using var database = await SqliteDatabaseMother.CreateAsync(
            TestContext.Current.CancellationToken
        );
        Product[] products =
        [
            .. Enumerable.Range(1, 7).Select(i => ProductMother.With($"P{i}", 5m)),
        ];
        await database.SeedAsync(products, TestContext.Current.CancellationToken);
        await using var dbContext = database.CreateContext();
        var repository = new ProductRepository(dbContext);
        ProductSort[] sort = [new(ProductSortField.Price, SortDirection.Ascending)];

        // Act
        var seen = new List<ProductId>();
        for (var pageNumber = 1; pageNumber <= 4; pageNumber++)
        {
            var page = await repository.GetPagedAsync(
                pageNumber,
                2,
                ProductCriteria.None,
                sort,
                TestContext.Current.CancellationToken
            );
            seen.AddRange(page.Items.Select(p => p.Id));
        }

        // Assert
        Assert.Equal(
            products.Select(p => p.Id).OrderBy(id => id.Value),
            seen.OrderBy(id => id.Value)
        );
    }

    /// <summary>Verifies an empty sort list means name ascending, and the result reports the sort that was applied.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetPagedAsync_NoSortKeys_AppliesAndReportsDefaultNameAscending_Test()
    {
        // Arrange
        Product[] seed = [ProductMother.With("Bravo"), ProductMother.With("Alpha")];

        // Act
        var page = await ListAsync(seed, ProductCriteria.None, NoSort);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(["Alpha", "Bravo"], page.Items.Select(p => p.Name)),
            () =>
                Assert.Equal(
                    [new ProductSort(ProductSortField.Name, SortDirection.Ascending)],
                    page.Sort
                )
        );
    }

    /// <summary>Verifies a requested sort is reported back exactly as applied, in order.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetPagedAsync_RequestedSort_IsReportedBackInOrder_Test()
    {
        // Arrange
        ProductSort[] sort =
        [
            new(ProductSortField.Price, SortDirection.Descending),
            new(ProductSortField.CreatedAt, SortDirection.Ascending),
        ];

        // Act
        var page = await ListAsync([ProductMother.With("A")], ProductCriteria.None, sort);

        // Assert
        Assert.Equal(sort, page.Sort);
    }

    /// <summary>Verifies paging applies to the filtered set: total counts only matches, and a later page slices within them.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetPagedAsync_FilteredAndPaged_SlicesWithinMatchesAndTotalsOnlyMatches_Test()
    {
        // Arrange
        Product[] seed =
        [
            .. Enumerable.Range(1, 5).Select(i => ProductMother.With($"Widget {i}", 5m)),
            ProductMother.With("Anvil", 5m),
            ProductMother.With("Widget Gold", 500m),
        ];
        var criteria = new ProductCriteria(NameContains: "widget", MaxPrice: 10m);

        // Act
        var page = await ListAsync(seed, criteria, NoSort, pageNumber: 2, pageSize: 2);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(["Widget 3", "Widget 4"], page.Items.Select(p => p.Name)),
            () => Assert.Equal(5, page.TotalCount),
            () => Assert.Equal(3, page.TotalPages)
        );
    }

    /// <summary>Verifies wildcard characters in the name filter are matched literally, not as patterns.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetPagedAsync_NameContainsWithWildcardCharacters_MatchesThemLiterally_Test()
    {
        // Arrange
        Product[] seed =
        [
            ProductMother.With("100% Cotton"),
            ProductMother.With("1000 Cotton"),
            ProductMother.With("A_B"),
            ProductMother.With("AXB"),
        ];

        // Act
        var percent = await ListAsync(seed, new ProductCriteria(NameContains: "0% c"), NoSort);
        var underscore = await ListAsync(seed, new ProductCriteria(NameContains: "a_b"), NoSort);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(["100% Cotton"], percent.Items.Select(p => p.Name)),
            () => Assert.Equal(["A_B"], underscore.Items.Select(p => p.Name))
        );
    }
}
