using MediatrUnionPoc.Application.Common.Abstractions;

namespace MediatrUnionPoc.Application.Features.Products.GetPaged;

/// <summary>Lists the products matching optional filters, in the requested order, a page at a time.</summary>
/// <param name="PageNumber">1-based page number. Must be at least 1.</param>
/// <param name="PageSize">Items per page. Must be between 1 and 100 inclusive.</param>
/// <param name="NameContains">Keep products whose name contains this text, ignoring case and surrounding whitespace; at most 200 characters. <see langword="null"/> means no name filter.</param>
/// <param name="MinPrice">Keep products priced at least this much (inclusive); not negative, and not above <paramref name="MaxPrice"/>. <see langword="null"/> means no lower bound.</param>
/// <param name="MaxPrice">Keep products priced at most this much (inclusive); not negative. <see langword="null"/> means no upper bound.</param>
/// <param name="OwnerId">Keep products owned by exactly this caller identifier; at most 200 characters. <see langword="null"/> means any owner.</param>
/// <param name="Sort">The sort specification, e.g. <c>name,-price</c>: comma-separated fields in priority order, a <c>-</c> prefix meaning descending; see <see cref="ProductSortParser"/> for the grammar. <see langword="null"/> or blank means name ascending.</param>
public sealed record GetPagedProductsQuery(
    int PageNumber,
    int PageSize,
    string? NameContains = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    string? OwnerId = null,
    string? Sort = null
) : IQuery<GetPagedProductsResult>;
