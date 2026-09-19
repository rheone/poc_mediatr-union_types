using MediatrUnionPoc.Application.Common.Abstractions;

namespace MediatrUnionPoc.Application.Features.Products.GetPaged;

/// <summary>Lists products a page at a time, ordered by name.</summary>
/// <param name="PageNumber">1-based page number. Must be at least 1.</param>
/// <param name="PageSize">Items per page. Must be between 1 and 100 inclusive.</param>
public sealed record GetPagedProductsQuery(int PageNumber, int PageSize)
    : IQuery<GetPagedProductsResult>;
