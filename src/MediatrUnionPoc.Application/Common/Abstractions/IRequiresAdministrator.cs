using System.Security.Claims;

namespace MediatrUnionPoc.Application.Common.Abstractions;

/// <summary>
/// A request that <see cref="Behaviors.AuthorizationBehavior{TRequest,TResponse}"/> gates behind
/// the <c>Administrator</c> policy before it reaches its handler. This POC has no real
/// authentication, so the caller's identity travels on the request itself — built by the
/// controller from a header — rather than being read off an ambient <c>HttpContext</c>.
/// </summary>
public interface IRequiresAdministrator
{
    /// <summary>The caller's identity, evaluated against the <c>Administrator</c> policy.</summary>
    ClaimsPrincipal Principal { get; }
}
