using System.Security.Claims;
using MediatrUnionPoc.Application.Common.Abstractions;
using MediatrUnionPoc.Application.Common.Authorization;

namespace MediatrUnionPoc.Application.Features.Products.Update;

/// <summary>Replaces a product's name and price in full — there is no partial-update support.</summary>
/// <param name="Id">The product's identity.</param>
/// <param name="Name">The product's new display name. Must be non-empty, at most 200 characters.</param>
/// <param name="Price">The product's new price. Must be zero or greater.</param>
/// <param name="Principal">
/// The caller's identity. Unlike <see cref="Delete.DeleteProductCommand.Principal"/>'s role-based
/// check (run by a pipeline behavior before the handler), only the product's owner may update it — a resource-based check against
/// <see cref="AuthorizationPolicies.ProductOwner"/> that <see cref="UpdateProductHandler"/> runs
/// itself, once it has loaded the product, via <see cref="ResourceAuthorizationService"/>. This
/// command deliberately does not implement <see cref="IRequiresAuthorization"/> — that pipeline
/// path runs before any resource is loaded, too early for an ownership check.
/// </param>
public sealed record UpdateProductCommand(
    Guid Id,
    string Name,
    decimal Price,
    ClaimsPrincipal Principal
) : ITransactionalCommand<UpdateProductResult>;
