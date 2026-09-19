using System.Security.Claims;

namespace MediatrUnionPoc.Application.Common.Abstractions;

/// <summary>
/// A request that <see cref="Behaviors.AuthorizationBehavior{TRequest,TResponse}"/> gates behind
/// a named ASP.NET Core authorization policy before it reaches its handler. This POC has no real
/// authentication, so the caller's identity travels on the request itself — built by the
/// controller from a header — rather than being read off an ambient <c>HttpContext</c>.
/// </summary>
public interface IRequiresAuthorization
{
    /// <summary>The caller's identity, evaluated against <see cref="PolicyName"/>.</summary>
    ClaimsPrincipal Principal { get; }

    /// <summary>
    /// The name of the registered authorization policy to evaluate <see cref="Principal"/>
    /// against — one <see cref="Microsoft.AspNetCore.Authorization.AuthorizationServiceExtensions.AuthorizeAsync(Microsoft.AspNetCore.Authorization.IAuthorizationService,ClaimsPrincipal,string)"/>
    /// recognizes, such as <see cref="Authorization.AuthorizationPolicies.Administrator"/>.
    /// </summary>
    string PolicyName { get; }
}
