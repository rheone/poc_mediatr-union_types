using System.Security.Claims;
using MediatrUnionPoc.Application.Common.Results;
using Microsoft.AspNetCore.Authorization;

namespace MediatrUnionPoc.Application.Common.Authorization;

/// <summary>
/// Resource-based counterpart to <see cref="Behaviors.AuthorizationBehavior{TRequest,TResponse}"/>,
/// callable from inside a handler once it has loaded the resource being acted on. Resource-based
/// checks are necessarily imperative — the resource typically has to come from a database before
/// it can be checked — so nothing runs before that the way pipeline behaviors run before
/// validation; this type exists to be called explicitly, after loading, rather than wired into the
/// MediatR pipeline.
/// </summary>
/// <remarks>
/// This service deliberately stops short of building the union's <c>NotAuthorized</c> case itself:
/// doing so needs <see cref="Abstractions.IAuthorizable{TSelf}"/> and therefore the concrete union
/// type, which only the calling handler knows and this generic helper does not need to. Instead it
/// returns a plain <see cref="Results.NotAuthorized"/> on failure (or <see langword="null"/> on
/// success), and the calling handler is expected to pass a non-null result straight to
/// <c>TResponse.FromNotAuthorized(...)</c> — the same shared case type and construction mechanism
/// <see cref="Behaviors.AuthorizationBehavior{TRequest,TResponse}"/> already converges on, just
/// invoked at a different point in the request's lifetime.
/// </remarks>
/// <param name="authorizationService">The framework authorization service resource-aware checks are delegated to.</param>
public sealed class ResourceAuthorizationService(IAuthorizationService authorizationService)
{
    /// <summary>
    /// Checks <paramref name="principal"/> against <paramref name="policyName"/> for the specific
    /// <paramref name="resource"/> the calling handler has already loaded, via
    /// <see cref="IAuthorizationService"/>'s resource-aware three-argument
    /// <c>AuthorizeAsync(principal, resource, policyName)</c> overload — not the policy-only
    /// overload <see cref="Behaviors.AuthorizationBehavior{TRequest,TResponse}"/> uses.
    /// </summary>
    /// <param name="principal">The caller to authorize.</param>
    /// <param name="resource">The already-loaded resource to authorize <paramref name="principal"/> against.</param>
    /// <param name="policyName">The name of the registered policy to evaluate.</param>
    /// <param name="cancellationToken">
    /// Accepted for signature consistency with this repo's other owned async methods; unused
    /// today because <see cref="IAuthorizationService"/> exposes no cancellable overload.
    /// </param>
    /// <returns>
    /// <see langword="null"/> when authorization succeeds; otherwise a <see cref="Results.NotAuthorized"/>
    /// describing the failure, for the caller to pass to its union's <c>FromNotAuthorized</c>.
    /// </returns>
    public async Task<NotAuthorized?> AuthorizeAsync(
        ClaimsPrincipal principal,
        object resource,
        string policyName,
        CancellationToken cancellationToken = default
    )
    {
        var result = await authorizationService.AuthorizeAsync(principal, resource, policyName);

        return result.Succeeded
            ? null
            : new NotAuthorized([
                $"The caller does not satisfy the {policyName} policy for this resource.",
            ]);
    }
}
