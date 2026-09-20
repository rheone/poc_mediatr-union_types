using System.Security.Claims;
using MediatrUnionPoc.Application.Common;
using MediatrUnionPoc.Application.Common.Abstractions;
using MediatrUnionPoc.Application.Common.Authorization;
using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Application.Features.Products.Patch;

/// <summary>
/// Changes only the fields of a product the caller supplied (JSON Merge Patch semantics, RFC 7396):
/// an absent <see cref="Optional{T}"/> leaves that field alone, a present one replaces it. There is
/// no way to "clear" a field — both are required on a product — so a present-but-<see langword="null"/>
/// value is a validation error, not a deletion.
/// </summary>
/// <param name="Id">The product's identity.</param>
/// <param name="Name">The new display name when present: non-empty, at most 200 characters.</param>
/// <param name="Price">The new price when present: not <see langword="null"/>, zero or greater.</param>
/// <param name="Principal">
/// The caller's identity. As for <see cref="Update.UpdateProductCommand.Principal"/>, only the
/// product's owner may change it — a resource-based check against
/// <see cref="AuthorizationPolicies.ProductOwner"/> that <see cref="PatchProductHandler"/> runs
/// itself once it has loaded the product. This command deliberately does not implement
/// <see cref="IRequiresAuthorization"/>: that pipeline path runs before any resource is loaded.
/// </param>
/// <param name="ExpectedVersion">
/// The <see cref="Domain.ProductVersion"/> the caller last saw (its <c>If-Match</c> ETag). The patch
/// only proceeds if the stored product is still at this version.
/// </param>
/// <exception cref="ArgumentNullException"><paramref name="Principal"/> is <see langword="null"/>.</exception>
public sealed record PatchProductCommand(
    Guid Id,
    Optional<string?> Name,
    Optional<decimal?> Price,
    ClaimsPrincipal Principal,
    ProductVersion ExpectedVersion
) : ITransactionalCommand<PatchProductResult>
{
    /// <summary>The caller's identity, checked by <see cref="PatchProductHandler"/> after loading the product; never <see langword="null"/>.</summary>
    public ClaimsPrincipal Principal { get; init; } =
        Principal ?? throw new ArgumentNullException(nameof(Principal));
}
