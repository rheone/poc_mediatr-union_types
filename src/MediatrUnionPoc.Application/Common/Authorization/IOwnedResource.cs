namespace MediatrUnionPoc.Application.Common.Authorization;

/// <summary>
/// A resource that can be checked by <see cref="OwnerAuthorizationHandler{TResource}"/> — any
/// resource shape exposing who owns it, independent of the resource's own domain type. A
/// consumer's entity either implements this directly, or is adapted to it (as
/// <c>OwnedProductResource</c> adapts <c>Product</c>), without
/// <see cref="OwnerAuthorizationHandler{TResource}"/> ever depending on that entity's assembly.
/// </summary>
public interface IOwnedResource
{
    /// <summary>
    /// The identifier of the caller who owns this resource — compared against the authenticated
    /// caller's <see cref="System.Security.Claims.ClaimTypes.NameIdentifier"/> claim.
    /// </summary>
    /// <value>An opaque caller identifier; must match the claim value exactly (ordinal comparison).</value>
    string OwnerId { get; }
}
