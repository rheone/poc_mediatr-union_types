using MediatR;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Application.Features.Products.Update;

/// <summary>
/// Updates a product's name and price. The <see cref="NotFound{TId}"/> case returned here is what
/// <see cref="MediatrUnionPoc.Application.Common.Behaviors.TransactionBehavior{TRequest,TResponse}"/>
/// treats as a rollback signal — no exception is thrown for a missing entity, since that's an
/// ordinary outcome of an update-by-id request, not a fault.
/// </summary>
public sealed class UpdateProductHandler(IProductRepository repository)
    : IRequestHandler<UpdateProductCommand, UpdateProductResult>
{
    /// <inheritdoc/>
    public async Task<UpdateProductResult> Handle(
        UpdateProductCommand request,
        CancellationToken cancellationToken
    )
    {
        var productId = ProductId.From(request.Id);
        var product = await repository.GetByIdAsync(productId, cancellationToken);

        if (product is null)
        {
            return new NotFound<ProductId>(productId);
        }

        product.UpdateDetails(request.Name, Money.From(request.Price));
        return new Success();
    }
}
