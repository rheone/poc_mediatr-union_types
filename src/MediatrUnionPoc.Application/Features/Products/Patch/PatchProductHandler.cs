using MediatR;
using MediatrUnionPoc.Application.Common.Authorization;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Products.Common;
using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Application.Features.Products.Patch;

/// <summary>
/// Applies only the supplied fields of a <see cref="PatchProductCommand"/> to a product. Loading,
/// the ownership check and the up-front version check are shared with the update handler
/// (<see cref="ProductChangeExtensions"/>); what is specific here is the duplicate-name check,
/// which runs only when the patch actually moves the product to a different name (comparing
/// <see cref="ProductNames.Normalize"/> forms, so re-casing or re-padding its own name is not a
/// conflict), and the single <see cref="Product.ApplyChanges"/> call, which advances the version
/// once. The unit-of-work pipeline behavior commits on <see cref="ProductDto"/> and no other case.
/// </summary>
/// <param name="repository">The repository the product is loaded from.</param>
/// <param name="resourceAuthorizationService">The service used for the resource-based ownership check once the product is loaded.</param>
/// <exception cref="ArgumentNullException"><paramref name="repository"/> or <paramref name="resourceAuthorizationService"/> is <see langword="null"/>.</exception>
public sealed class PatchProductHandler(
    IProductRepository repository,
    ResourceAuthorizationService resourceAuthorizationService
) : IRequestHandler<PatchProductCommand, PatchProductResult>
{
    private readonly IProductRepository _repository =
        repository ?? throw new ArgumentNullException(nameof(repository));

    private readonly ResourceAuthorizationService _resourceAuthorizationService =
        resourceAuthorizationService
        ?? throw new ArgumentNullException(nameof(resourceAuthorizationService));

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    public async Task<PatchProductResult> Handle(
        PatchProductCommand request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        var productId = ProductId.From(request.Id);
        var loaded = await _repository.LoadForChangeAsync(
            _resourceAuthorizationService,
            productId,
            request.Principal,
            request.ExpectedVersion,
            cancellationToken
        );

        Product product;
        switch (loaded)
        {
            case Product cleared:
                product = cleared;
                break;
            case NotFound<ProductId> notFound:
                return notFound;
            case NotAuthorized notAuthorized:
                return PatchProductResult.FromNotAuthorized(notAuthorized);
            case PreconditionFailed stale:
                return stale;
            default:
                throw new InvalidOperationException("Unreachable: the union has four cases.");
        }

        var newName = request.Name.GetValueOrDefault();

        if (
            newName is not null
            && ProductNames.Normalize(newName) != product.NormalizedName
            && await _repository.ExistsWithNameAsync(newName, productId, cancellationToken)
        )
        {
            return ProductConflicts.NameTaken(newName);
        }

        product.ApplyChanges(
            newName,
            request.Price.GetValueOrDefault() is { } price ? Money.From(price) : null
        );
        return ProductDto.FromDomain(product);
    }
}
