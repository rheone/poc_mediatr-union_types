using MediatR;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Application.Features.Products.Delete;

/// <summary>
/// Deletes a product. <see cref="IProductRepository.Remove"/> only stages the deletion against the change tracker —
/// nothing is actually removed from the database unless
/// <see cref="MediatrUnionPoc.Application.Common.Behaviors.TransactionBehavior{TRequest,TResponse}"/>
/// commits afterwards, which it does only for this handler's <see cref="Success"/> case. Who may
/// delete is decided before this handler runs, by <see cref="MediatrUnionPoc.Application.Common.Behaviors.AuthorizationBehavior{TRequest,TResponse}"/>
/// (see <see cref="DeleteProductCommand"/>).
/// </summary>
/// <param name="repository">The repository the product is loaded from.</param>
/// <exception cref="ArgumentNullException"><paramref name="repository"/> is <see langword="null"/>.</exception>
public sealed class DeleteProductHandler(IProductRepository repository)
    : IRequestHandler<DeleteProductCommand, DeleteProductResult>
{
    private readonly IProductRepository _repository =
        repository ?? throw new ArgumentNullException(nameof(repository));

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    public async Task<DeleteProductResult> Handle(
        DeleteProductCommand request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        var productId = ProductId.From(request.Id);
        var product = await _repository.GetByIdAsync(productId, cancellationToken);

        if (product is null)
        {
            return new NotFound<ProductId>(productId);
        }

        if (request.ExpectedVersion is { } expected && product.Version != expected)
        {
            return new PreconditionFailed(
                $"Product '{productId}' is at version {product.Version.Value}, not the expected {expected.Value}."
            );
        }

        _repository.Remove(product);
        return new Success();
    }
}
