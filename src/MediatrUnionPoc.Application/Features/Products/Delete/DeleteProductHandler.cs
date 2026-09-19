using MediatR;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Application.Features.Products.Delete;

/// <summary>
/// Deletes a product. <see cref="IProductRepository.Remove"/> only stages the deletion against the change tracker —
/// nothing is actually removed from the database unless
/// <see cref="MediatrUnionPoc.Application.Common.Behaviors.TransactionBehavior{TRequest,TResponse}"/>
/// commits afterwards, which it does only for this handler's <see cref="Success"/> case.
/// </summary>
public sealed class DeleteProductHandler(IProductRepository repository)
    : IRequestHandler<DeleteProductCommand, DeleteProductResult>
{
    /// <inheritdoc/>
    public async Task<DeleteProductResult> Handle(
        DeleteProductCommand request,
        CancellationToken cancellationToken
    )
    {
        var productId = ProductId.From(request.Id);
        var product = await repository.GetByIdAsync(productId, cancellationToken);

        if (product is null)
        {
            return new NotFound<ProductId>(productId);
        }

        repository.Remove(product);
        return new Success();
    }
}
