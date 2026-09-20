using System.Diagnostics;

namespace MediatrUnionPoc.Domain;

/// <summary>A single page of <typeparamref name="T"/>, plus enough metadata for a caller to compute whether further pages exist.</summary>
/// <typeparam name="T">The type of item being paged.</typeparam>
/// <param name="Items">The rows for this page only — never the full result set. Must not be <see langword="null"/>; a null value throws <see cref="ArgumentNullException"/> on construction or in a <c>with</c> expression.</param>
/// <param name="PageNumber">The 1-based page number this result represents.</param>
/// <param name="PageSize">The page size that was requested, not necessarily <see cref="Items"/>'s count (the last page is typically shorter).</param>
/// <param name="TotalCount">The total number of rows across all pages, used to compute <see cref="TotalPages"/>.</param>
/// <param name="Sort">The sort keys that were applied, in priority order (the implicit trailing <see cref="Product.Id"/> tiebreaker is not listed).</param>
[DebuggerDisplay("Page {PageNumber}/{TotalPages}, {Items.Count} of {TotalCount} items")]
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    IReadOnlyList<ProductSort> Sort
)
{
    /// <summary>The rows for this page only — never the full result set.</summary>
    /// <value>The non-null page rows.</value>
    /// <exception cref="ArgumentNullException">The value supplied at construction or through a <c>with</c> expression is <see langword="null"/>.</exception>
    public IReadOnlyList<T> Items
    {
        get => _items;
        init => _items = value ?? throw new ArgumentNullException(nameof(Items));
    }

    private readonly IReadOnlyList<T> _items =
        Items ?? throw new ArgumentNullException(nameof(Items));

    /// <summary>The number of the first page.</summary>
    /// <value>Always 1; pages are 1-based.</value>
    public int FirstPage => 1;

    /// <summary>The number of the last page at the current <see cref="PageSize"/>.</summary>
    /// <value><see cref="TotalPages"/>, but never less than 1: a result with no rows still has one (empty) page.</value>
    public int LastPage => Math.Max(1, TotalPages);

    /// <summary>The next page's number.</summary>
    /// <value><see langword="null"/> when <see cref="PageNumber"/> is the last page or beyond it.</value>
    public int? NextPage => PageNumber < LastPage ? PageNumber + 1 : null;

    /// <summary>The previous page's number.</summary>
    /// <value><see langword="null"/> on the first page. From a page past the end, the real <see cref="LastPage"/>, so a client can always navigate back to data.</value>
    public int? PreviousPage => PageNumber > FirstPage ? Math.Min(PageNumber - 1, LastPage) : null;

    /// <summary>The total number of pages, rounded up — e.g. 10 total rows at a page size of 3 yields 4 pages, the last with only 1 row.</summary>
    /// <value>The page count implied by <see cref="TotalCount"/> and <see cref="PageSize"/>; never negative, but callers must guard against a zero <see cref="PageSize"/>.</value>
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
