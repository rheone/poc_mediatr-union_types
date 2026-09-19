using MediatrUnionPoc.Application.Common.Authorization;
using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Application.Features.Products.Common;

/// <summary>
/// Adapts an already-loaded domain <see cref="Product"/> to <see cref="IOwnedResource"/> so it can
/// be checked by <see cref="OwnerAuthorizationHandler{TResource}"/>. <see cref="Product"/> cannot
/// implement <see cref="IOwnedResource"/> directly — Domain must not depend on Application (see
/// <c>MediatrUnionPoc.ArchitectureTests.LayeringTests</c>) — so this thin wrapper is the
/// adaptation <see cref="IOwnedResource"/>'s own doc comment anticipates, rather than a stub.
/// </summary>
/// <param name="OwnerId">The wrapped product's owner identifier.</param>
public sealed record OwnedProductResource(string OwnerId) : IOwnedResource
{
    /// <summary>Wraps an already-loaded <see cref="Product"/> for a resource-based authorization check.</summary>
    /// <param name="product">The already-loaded product to wrap.</param>
    /// <returns>An <see cref="OwnedProductResource"/> exposing <paramref name="product"/>'s owner.</returns>
    public static OwnedProductResource FromDomain(Product product) => new(product.OwnerId);
}
