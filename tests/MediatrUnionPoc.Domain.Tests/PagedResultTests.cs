namespace MediatrUnionPoc.Domain.Tests;

/// <summary>
/// Verifies <see cref="PagedResult{T}.TotalPages"/>'s rounding-up computation, which has no
/// producer-side test of its own — every handler test that builds a <see cref="PagedResult{T}"/>
/// picks values where the rounding behavior isn't the point.
/// </summary>
public class PagedResultTests
{
    /// <summary>Verifies a total count that divides evenly by the page size needs no extra page.</summary>
    [Fact]
    public void TotalPages_is_exact_when_total_count_divides_evenly_by_page_size()
    {
        var page = new PagedResult<string>(Items: [], PageNumber: 1, PageSize: 5, TotalCount: 10);

        Assert.Equal(2, page.TotalPages);
    }

    /// <summary>Verifies a remainder still counts as one more page.</summary>
    [Fact]
    public void TotalPages_rounds_up_when_the_last_page_is_partial()
    {
        var page = new PagedResult<string>(Items: [], PageNumber: 1, PageSize: 3, TotalCount: 10);

        Assert.Equal(4, page.TotalPages);
    }

    /// <summary>Verifies no rows at all still reports zero pages rather than rounding up to one.</summary>
    [Fact]
    public void TotalPages_is_zero_when_there_are_no_rows()
    {
        var page = new PagedResult<string>(Items: [], PageNumber: 1, PageSize: 10, TotalCount: 0);

        Assert.Equal(0, page.TotalPages);
    }
}
