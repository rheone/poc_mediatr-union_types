using System.Diagnostics;

namespace MediatrUnionPoc.Domain;

/// <summary>A single page of <typeparamref name="T"/>, plus enough metadata for a caller to compute whether further pages exist.</summary>
/// <typeparam name="T">The type of item being paged.</typeparam>
/// <param name="Items">The rows for this page only — never the full result set.</param>
/// <param name="PageNumber">The 1-based page number this result represents.</param>
/// <param name="PageSize">The page size that was requested, not necessarily <see cref="Items"/>'s count (the last page is typically shorter).</param>
/// <param name="TotalCount">The total number of rows across all pages, used to compute <see cref="TotalPages"/>.</param>
[DebuggerDisplay("Page {PageNumber}/{TotalPages}, {Items.Count} of {TotalCount} items")]
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int PageNumber,
    int PageSize,
    int TotalCount
)
{
    /// <summary>The total number of pages, rounded up — e.g. 10 total rows at a page size of 3 yields 4 pages, the last with only 1 row.</summary>
    /// <value>The page count implied by <see cref="TotalCount"/> and <see cref="PageSize"/>; never negative, but callers must guard against a zero <see cref="PageSize"/>.</value>
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
