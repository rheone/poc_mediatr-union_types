using System.Security.Claims;
using MediatrUnionPoc.Application.Common.Abstractions;
using MediatrUnionPoc.Application.Common.Authorization;

namespace MediatrUnionPoc.Application.Features.Products.Delete;

/// <summary>
/// Deletes a product by id. Idempotent in effect but not in result — deleting an already-missing
/// product still returns <c>NotFound</c>, not <c>Success</c>. Either the product's owner or an
/// administrator may delete it — a resource-based check against
/// <see cref="AuthorizationPolicies.ProductOwnerOrAdministrator"/> that
/// <see cref="DeleteProductHandler"/> runs itself, once it has loaded the product, via
/// <see cref="ResourceAuthorizationService"/>. This command deliberately does not implement
/// <see cref="IRequiresAuthorization"/> — that pipeline path runs before any resource is loaded, too
/// early to know whether the caller owns this particular product, and the admin bypass rides along
/// on the same resource-based policy (see <see cref="AuthorizationPolicies.ProductOwnerOrAdministrator"/>
/// for how "owner OR admin" is expressed as two independently-registered handlers rather than
/// application-level OR logic).
/// </summary>
/// <param name="Id">The product's identity.</param>
/// <param name="Principal">The caller's identity, checked by <see cref="DeleteProductHandler"/> after loading the product.</param>
public sealed record DeleteProductCommand(Guid Id, ClaimsPrincipal Principal)
    : ITransactionalCommand<DeleteProductResult>;
