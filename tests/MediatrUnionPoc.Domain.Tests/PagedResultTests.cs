namespace MediatrUnionPoc.Domain.Tests;

/// <summary>
/// Verifies <see cref="PagedResult{T}.TotalPages"/>'s rounding-up computation, which has no
/// producer-side test of its own — every handler test that builds a <see cref="PagedResult{T}"/>
/// picks values where the rounding behavior isn't the point.
/// </summary>
public class PagedResultTests
{
    /// <summary>Verifies <see cref="PagedResult{T}.TotalPages"/> rounds up to the nearest whole page, including the zero-rows edge case.</summary>
    /// <param name="pageSize">The requested page size.</param>
    /// <param name="totalCount">The total row count across all pages.</param>
    /// <param name="expectedTotalPages">The expected value of <see cref="PagedResult{T}.TotalPages"/> for this combination.</param>
    [Theory]
    [InlineData(5, 10, 2)] // divides evenly: no extra page needed
    [InlineData(3, 10, 4)] // remainder still counts as one more page
    [InlineData(10, 0, 0)] // no rows: zero pages, not one
    public void TotalPages_rounds_up_to_the_nearest_whole_page(
        int pageSize,
        int totalCount,
        int expectedTotalPages
    )
    {
        var page = new PagedResult<string>(
            Items: [],
            PageNumber: 1,
            PageSize: pageSize,
            TotalCount: totalCount
        );

        Assert.Equal(expectedTotalPages, page.TotalPages);
    }
}
