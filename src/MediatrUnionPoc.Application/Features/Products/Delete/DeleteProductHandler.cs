using MediatR;
using MediatrUnionPoc.Application.Common.Authorization;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Products.Common;
using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Application.Features.Products.Delete;

/// <summary>
/// Deletes a product. <see cref="IProductRepository.Remove"/> only stages the deletion against the change tracker —
/// nothing is actually removed from the database unless
/// <see cref="MediatrUnionPoc.Application.Common.Behaviors.TransactionBehavior{TRequest,TResponse}"/>
/// commits afterwards, which it does only for this handler's <see cref="Success"/> case. Once the
/// product is loaded, either its owner or an administrator may proceed — checked via
/// <see cref="ResourceAuthorizationService"/> against <see cref="AuthorizationPolicies.ProductOwnerOrAdministrator"/>,
/// the same resource-based mechanism <see cref="Update.UpdateProductHandler"/> uses, but backed by
/// two independently-registered handlers (ownership and an admin role bypass) instead of one.
/// </summary>
public sealed class DeleteProductHandler(
    IProductRepository repository,
    ResourceAuthorizationService resourceAuthorizationService
) : IRequestHandler<DeleteProductCommand, DeleteProductResult>
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

        var notAuthorized = await resourceAuthorizationService.AuthorizeAsync(
            request.Principal,
            OwnedProductResource.FromDomain(product),
            AuthorizationPolicies.ProductOwnerOrAdministrator,
            cancellationToken
        );

        if (notAuthorized is not null)
        {
            return DeleteProductResult.FromNotAuthorized(notAuthorized);
        }

        repository.Remove(product);
        return new Success();
    }
}
