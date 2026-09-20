using System.Security.Claims;
using MediatrUnionPoc.Application.Common.Authorization;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Application.Features.Products.Common;

/// <summary>
/// C# 14 extension members for the steps every "change an existing product" handler starts with,
/// in the order they must run: load, check ownership, check the caller's version is still current.
/// </summary>
internal static class ProductChangeExtensions
{
    extension(IProductRepository repository)
    {
        /// <summary>
        /// Loads a product and clears it for a change by <paramref name="principal"/>: it must exist,
        /// be owned by the caller (<see cref="AuthorizationPolicies.ProductOwner"/>), and still be at
        /// <paramref name="expectedVersion"/>. Nothing is mutated.
        /// </summary>
        /// <param name="authorization">The service that runs the resource-based ownership check.</param>
        /// <param name="id">The product to load.</param>
        /// <param name="principal">The caller.</param>
        /// <param name="expectedVersion">The version the caller last saw.</param>
        /// <param name="cancellationToken">A token to cancel the load.</param>
        /// <returns>The product when cleared; otherwise the first reason it is not, in that order.</returns>
        public async Task<LoadedForChangeResult> LoadForChangeAsync(
            ResourceAuthorizationService authorization,
            ProductId id,
            ClaimsPrincipal principal,
            ProductVersion expectedVersion,
            CancellationToken cancellationToken = default
        )
        {
            var product = await repository.GetByIdAsync(id, cancellationToken);

            if (product is null)
            {
                return new NotFound<ProductId>(id);
            }

            var notAuthorized = await authorization.AuthorizeAsync(
                principal,
                OwnedProductResource.FromDomain(product),
                AuthorizationPolicies.ProductOwner,
                cancellationToken
            );

            if (notAuthorized is not null)
            {
                return notAuthorized;
            }

            return product.Version != expectedVersion
                ? new PreconditionFailed(
                    $"Product '{id}' is at version {product.Version.Value}, not the expected {expectedVersion.Value}."
                )
                : product;
        }
    }
}
