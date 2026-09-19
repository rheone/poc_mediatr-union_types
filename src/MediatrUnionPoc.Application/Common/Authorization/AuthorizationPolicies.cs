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
    /// Requires the caller to either own the resource being acted on or hold the
    /// <c>Administrator</c> role — evaluated as an OR across two independently-registered
    /// <see cref="Microsoft.AspNetCore.Authorization.IAuthorizationHandler"/>s answering the same
    /// <see cref="Microsoft.AspNetCore.Authorization.Infrastructure.OperationAuthorizationRequirement"/>:
    /// <see cref="OwnerAuthorizationHandler{TResource}"/> (ownership) and
    /// <see cref="AdministratorResourceOverrideAuthorizationHandler{TResource}"/> (the role bypass,
    /// registered for this policy's <c>Delete</c> operation name only — <see cref="ProductOwner"/>'s
    /// <c>Update</c> operation is unaffected by it). No OR logic lives in application code for
    /// this; ASP.NET Core's own evaluation succeeds a requirement as soon as any one registered
    /// handler succeeds it. Checked the same way <see cref="ProductOwner"/> is: explicitly, from
    /// inside a handler, via <see cref="ResourceAuthorizationService"/>, once the resource has been
    /// loaded.
    /// </summary>
    public const string ProductOwnerOrAdministrator = "ProductOwnerOrAdministrator";
}
