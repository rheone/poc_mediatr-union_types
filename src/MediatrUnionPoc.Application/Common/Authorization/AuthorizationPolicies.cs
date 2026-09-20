namespace MediatrUnionPoc.Application.Common.Authorization;

/// <summary>Names of the authorization policies registered by <see cref="MediatrUnionPoc.Application.DependencyInjection.AddApplication"/>.</summary>
public static class AuthorizationPolicies
{
    /// <summary>
    /// Requires the caller to hold the <c>Administrator</c> role — checked by
    /// <see cref="Behaviors.AuthorizationBehavior{TRequest,TResponse}"/> for every request
    /// implementing <see cref="Abstractions.IRequiresAuthorization"/> with this as its
    /// <see cref="Abstractions.IRequiresAuthorization.PolicyName"/>.
    /// </summary>
    public const string Administrator = "Administrator";

    /// <summary>
    /// Requires the caller to own the resource being acted on — evaluated by
    /// <see cref="OwnerAuthorizationHandler{TResource}"/> against an
    /// <see cref="Microsoft.AspNetCore.Authorization.Infrastructure.OperationAuthorizationRequirement"/>,
    /// called explicitly from inside a handler via <see cref="ResourceAuthorizationService"/> once
    /// the resource has been loaded — unlike <see cref="Administrator"/>, this is never checked by
    /// <see cref="Behaviors.AuthorizationBehavior{TRequest,TResponse}"/>, since that pipeline
    /// behavior runs before any resource is loaded.
    /// </summary>
    public const string ProductOwner = "ProductOwner";

    /// <summary>
    /// Requires the caller to hold the <c>Administrator</c> or the <c>Support</c> role, checked by
    /// <see cref="Behaviors.AuthorizationBehavior{TRequest,TResponse}"/> for the request that mints an
    /// impersonation token. Holding the role only lets a caller <em>attempt</em> to impersonate;
    /// which identities and roles they may then grant is decided by the request's handler.
    /// </summary>
    public const string Impersonator = "Impersonator";
}
