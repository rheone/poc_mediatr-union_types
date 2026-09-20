namespace MediatrUnionPoc.Domain.Tests;

/// <summary>
/// Verifies <see cref="PagedResult{T}.TotalPages"/>'s rounding-up computation, which has no
/// producer-side test of its own — every handler test that builds a <see cref="PagedResult{T}"/>
/// picks values where the rounding behavior isn't the point.
/// </summary>
public class PagedResultTests
{
    /// <summary>Rows: even division, remainder (rounds up), zero rows (zero pages, not one).</summary>
    public static TheoryData<
        int,
        int,
        int
    > TotalPages_VariousCounts_RoundsUpToWholePages_Test_Data =>
        new()
        {
            { 5, 10, 2 }, // divides evenly: no extra page needed
            { 3, 10, 4 }, // remainder still counts as one more page
            { 10, 0, 0 }, // no rows: zero pages, not one
        };

    /// <summary>Verifies <see cref="PagedResult{T}.TotalPages"/> rounds up to the nearest whole page, including the zero-rows edge case.</summary>
    /// <param name="pageSize">The requested page size.</param>
    /// <param name="totalCount">The total row count across all pages.</param>
    /// <param name="expectedTotalPages">The expected value of <see cref="PagedResult{T}.TotalPages"/> for this combination.</param>
    [Theory]
    [MemberData(nameof(TotalPages_VariousCounts_RoundsUpToWholePages_Test_Data))]
    public void TotalPages_VariousCounts_RoundsUpToWholePages_Test(
        int pageSize,
        int totalCount,
        int expectedTotalPages
    )
    {
        // Arrange
        var page = new PagedResult<string>(
            Items: [],
            PageNumber: 1,
            PageSize: pageSize,
            TotalCount: totalCount,
            Sort: ProductSort.Default
        );

        // Act
        var totalPages = page.TotalPages;

        // Assert
        Assert.Equal(expectedTotalPages, totalPages);
    }

    /// <summary>
    /// Rows: 25 items at size 10 is 3 pages. Columns: page number, then the expected last, next and previous pages
    /// (0 stands for "none"). First page has no previous; last page has no next; a page past the end has no next and
    /// points back at the real last page.
    /// </summary>
    public static TheoryData<int, int, int, int> Navigation_TwentyFiveItemsSizeTen_Test_Data =>
        new()
        {
            { 1, 3, 2, 0 },
            { 2, 3, 3, 1 },
            { 3, 3, 0, 2 },
            { 9, 3, 0, 3 },
        };

    /// <summary>Verifies the page-navigation values for a 25-item result at page size 10 (3 pages).</summary>
    /// <param name="pageNumber">The current page.</param>
    /// <param name="expectedLast">The expected last page.</param>
    /// <param name="expectedNext">The expected next page, 0 when none.</param>
    /// <param name="expectedPrevious">The expected previous page, 0 when none.</param>
    [Theory]
    [MemberData(nameof(Navigation_TwentyFiveItemsSizeTen_Test_Data))]
    public void Navigation_TwentyFiveItemsSizeTen_ReportsFirstLastNextPrevious_Test(
        int pageNumber,
        int expectedLast,
        int expectedNext,
        int expectedPrevious
    )
    {
        // Arrange
        var page = new PagedResult<string>([], pageNumber, 10, 25, ProductSort.Default);

        // Act / Assert
        Assert.Multiple(
            () => Assert.Equal(1, page.FirstPage),
            () => Assert.Equal(expectedLast, page.LastPage),
            () => Assert.Equal(expectedNext == 0 ? null : expectedNext, page.NextPage),
            () => Assert.Equal(expectedPrevious == 0 ? null : expectedPrevious, page.PreviousPage)
        );
    }

    /// <summary>Verifies an empty result still has one (empty) first-and-last page and no neighbours.</summary>
    [Fact]
    public void Navigation_NoRows_HasSingleEmptyPageAndNoNeighbours_Test()
    {
        // Arrange
        var page = new PagedResult<string>([], 1, 10, 0, ProductSort.Default);

        // Act / Assert
        Assert.Multiple(
            () => Assert.Equal(0, page.TotalPages),
            () => Assert.Equal(1, page.FirstPage),
            () => Assert.Equal(1, page.LastPage),
            () => Assert.Null(page.NextPage),
            () => Assert.Null(page.PreviousPage)
        );
    }

    /// <summary>Verifies <see cref="PagedResult{T}"/> rejects null items.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void Ctor_NullItems_ThrowsArgumentNullException_Test()
    {
        // Arrange / Act
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new PagedResult<string>(
                Items: null!,
                PageNumber: 1,
                PageSize: 10,
                TotalCount: 0,
                Sort: ProductSort.Default
            )
        );

        // Assert
        Assert.Equal("Items", ex.ParamName);
    }

    /// <summary>Verifies a <c>with</c> expression cannot smuggle null into <see cref="PagedResult{T}.Items"/>.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void With_NullItems_ThrowsArgumentNullException_Test()
    {
        // Arrange
        var page = new PagedResult<string>(
            Items: [],
            PageNumber: 1,
            PageSize: 10,
            TotalCount: 0,
            Sort: ProductSort.Default
        );

        // Act
        var ex = Assert.Throws<ArgumentNullException>(() => page with { Items = null! });

        // Assert
        Assert.Equal("Items", ex.ParamName);
    }

    /// <summary>Verifies a valid <c>with</c> expression still copies the record with the changed member and preserves value equality.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void With_ValidPageNumber_CopiesRecordAndPreservesEquality_Test()
    {
        // Arrange
        IReadOnlyList<string> items = ["a", "b"];
        var page = new PagedResult<string>(
            items,
            PageNumber: 1,
            PageSize: 2,
            TotalCount: 4,
            Sort: ProductSort.Default
        );

        // Act
        var next = page with
        {
            PageNumber = 2,
        };
        var same = page with { };

        // Assert
        Assert.Equal(2, next.PageNumber);
        Assert.Same(items, next.Items);
        Assert.NotEqual(page, next);
        Assert.Equal(page, same);
        Assert.Equal(page.GetHashCode(), same.GetHashCode());
    }

    // PageSize 0 is deliberately not guarded here: TotalPages documents it as a caller precondition,
    // enforced upstream by GetPagedProductsValidator.
}
