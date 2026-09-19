using MediatR;
using MediatrUnionPoc.Application.Features.Products.Common;
using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Application.Features.Products.GetPaged;

/// <summary>
/// Lists products a page at a time, ordered by name. Note that <see cref="GetPagedProductsResult"/>
/// doesn't declare a <c>ValidationErrors</c> case at all — <see cref="GetPagedProductsValidator"/>
/// failures are mapped into its <c>Error</c> case instead, via
/// <see cref="GetPagedProductsResult.FromValidationErrors"/>. Past that point this handler always
/// succeeds: there is no entity to be "not found" for a list, so an out-of-range page simply yields
/// a <see cref="PagedResult{T}"/> with an empty <see cref="PagedResult{T}.Items"/> collection.
/// </summary>
public sealed class GetPagedProductsHandler(IProductRepository repository)
    : IRequestHandler<GetPagedProductsQuery, GetPagedProductsResult>
{
    /// <inheritdoc/>
    public async Task<GetPagedProductsResult> Handle(
        GetPagedProductsQuery request,
        CancellationToken cancellationToken
    )
    {
        var page = await repository.GetPagedAsync(
            request.PageNumber,
            request.PageSize,
            cancellationToken
        );

        return new PagedResult<ProductDto>(
            page.Items.Select(ProductDto.FromDomain).ToList(),
            page.PageNumber,
            page.PageSize,
            page.TotalCount
        );
    }
}
