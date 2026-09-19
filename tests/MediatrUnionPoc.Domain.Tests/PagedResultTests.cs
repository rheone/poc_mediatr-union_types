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
            TotalCount: totalCount
        );

        // Act
        var totalPages = page.TotalPages;

        // Assert
        Assert.Equal(expectedTotalPages, totalPages);
    }

    /// <summary>Verifies <see cref="PagedResult{T}"/> rejects null items.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void Ctor_NullItems_ThrowsArgumentNullException_Test()
    {
        // Arrange / Act
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new PagedResult<string>(Items: null!, PageNumber: 1, PageSize: 10, TotalCount: 0)
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
        var page = new PagedResult<string>(Items: [], PageNumber: 1, PageSize: 10, TotalCount: 0);

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
        var page = new PagedResult<string>(items, PageNumber: 1, PageSize: 2, TotalCount: 4);

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
