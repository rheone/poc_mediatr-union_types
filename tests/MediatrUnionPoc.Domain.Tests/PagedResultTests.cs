namespace MediatrUnionPoc.Domain.Tests;

/// <summary>
/// Verifies <see cref="PagedResult{T}.TotalPages"/>'s rounding-up computation, which has no
/// producer-side test of its own — every handler test that builds a <see cref="PagedResult{T}"/>
/// picks values where the rounding behavior isn't the point.
/// </summary>
public class PagedResultTests
{
    /// <summary>Rows: even division, remainder (rounds up), zero rows (zero pages, not one).</summary>
    public static TheoryData<int, int, int> TotalPages_rounds_up_Test_Data =>
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
    [MemberData(nameof(TotalPages_rounds_up_Test_Data))]
    public void TotalPages_rounds_up_to_the_nearest_whole_page(
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
}
