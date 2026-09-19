using MediatR;
using MediatrUnionPoc.Application.Features.Products.Common;
using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Application.Features.Products.GetPaged;

/// <summary>Lists products a page at a time. Always succeeds once past validation — there is no entity to be "not found" for a list.</summary>
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
