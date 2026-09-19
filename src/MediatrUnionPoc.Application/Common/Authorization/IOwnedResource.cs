namespace MediatrUnionPoc.Application.Common.Authorization;

/// <summary>
/// A resource that can be checked by <see cref="OwnerAuthorizationHandler{TResource}"/> — any
/// resource shape exposing who owns it, independent of the resource's own domain type. A real
/// consumer's entity (e.g. a future <c>Product</c>-backed resource) implements this directly, or
/// is adapted to it, without <see cref="OwnerAuthorizationHandler{TResource}"/> ever depending on
/// that entity's assembly.
/// </summary>
public interface IOwnedResource
{
    /// <summary>
    /// The identifier of the caller who owns this resource — compared against the authenticated
    /// caller's <see cref="System.Security.Claims.ClaimTypes.NameIdentifier"/> claim.
    /// </summary>
    string OwnerId { get; }
}
