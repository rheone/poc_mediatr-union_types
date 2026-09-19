using MediatR;
using MediatrUnionPoc.Application.Common.Authorization;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Products.Common;
using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Application.Features.Products.Update;

/// <summary>
/// Updates a product's name and price. The <see cref="NotFound{TId}"/> case returned here is what
/// <see cref="MediatrUnionPoc.Application.Common.Behaviors.TransactionBehavior{TRequest,TResponse}"/>
/// treats as a rollback signal — no exception is thrown for a missing entity, since that's an
/// ordinary outcome of an update-by-id request, not a fault. Once the product is loaded, only its
/// owner may proceed — checked via <see cref="ResourceAuthorizationService"/>, the resource-based
/// counterpart to the role-based check <see cref="Delete.DeleteProductHandler"/>'s command has
/// already passed through the pipeline before its own handler runs (see
/// <see cref="UpdateProductCommand.Principal"/> for why this one runs here instead).
/// </summary>
/// <param name="repository">The repository the product is loaded from.</param>
/// <param name="resourceAuthorizationService">The service used for the resource-based authorization check once the product is loaded.</param>
/// <exception cref="ArgumentNullException"><paramref name="repository"/> or <paramref name="resourceAuthorizationService"/> is <see langword="null"/>.</exception>
public sealed class UpdateProductHandler(
    IProductRepository repository,
    ResourceAuthorizationService resourceAuthorizationService
) : IRequestHandler<UpdateProductCommand, UpdateProductResult>
{
    private readonly IProductRepository _repository =
        repository ?? throw new ArgumentNullException(nameof(repository));
    private readonly ResourceAuthorizationService _resourceAuthorizationService =
        resourceAuthorizationService
        ?? throw new ArgumentNullException(nameof(resourceAuthorizationService));

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    public async Task<UpdateProductResult> Handle(
        UpdateProductCommand request,
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

        var notAuthorized = await _resourceAuthorizationService.AuthorizeAsync(
            request.Principal,
            OwnedProductResource.FromDomain(product),
            AuthorizationPolicies.ProductOwner,
            cancellationToken
        );

        if (notAuthorized is not null)
        {
            return UpdateProductResult.FromNotAuthorized(notAuthorized);
        }

        product.UpdateDetails(request.Name, Money.From(request.Price));
        return new Success();
    }
}
