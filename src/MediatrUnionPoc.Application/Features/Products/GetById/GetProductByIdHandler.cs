using MediatR;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Products.Common;
using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Application.Features.Products.GetById;

/// <summary>
/// Looks up a product by id. Note that <see cref="GetProductByIdResult"/> doesn't declare a
/// <c>ValidationErrors</c> case at all — <see cref="GetProductByIdValidator"/> failures are mapped
/// into its <c>Error</c> case instead, via <see cref="GetProductByIdResult.FromValidationErrors"/>.
/// </summary>
public sealed class GetProductByIdHandler(IProductRepository repository)
    : IRequestHandler<GetProductByIdQuery, GetProductByIdResult>
{
    /// <inheritdoc/>
    public async Task<GetProductByIdResult> Handle(
        GetProductByIdQuery request,
        CancellationToken cancellationToken
    )
    {
        var productId = ProductId.From(request.Id);
        var product = await repository.GetByIdAsync(productId, cancellationToken);

        return product is null
            ? new NotFound<ProductId>(productId)
            : ProductDto.FromDomain(product);
    }
}
