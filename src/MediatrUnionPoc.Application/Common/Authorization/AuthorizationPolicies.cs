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
}
