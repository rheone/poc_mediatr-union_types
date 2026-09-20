using System.Security.Claims;
using MediatrUnionPoc.Application.Common.Authorization;

namespace MediatrUnionPoc.Api.Logging;

/// <summary>
/// The per-request properties that describe the caller on a log event: who they are, and whether
/// they are acting through an impersonation token and for whom. Read from the principal only, and
/// only these three values; nothing from the token itself (not the token string, not the reason,
/// not any header) is ever copied onto a log event.
/// </summary>
public static class RequestLogProperties
{
    /// <summary>The property carrying the authenticated caller's id (<c>NameIdentifier</c>).</summary>
    public const string UserId = nameof(UserId);

    /// <summary>The property that is <see langword="true"/> when the caller uses an impersonation token.</summary>
    public const string IsImpersonated = nameof(IsImpersonated);

    /// <summary>The property carrying the real caller's id (the <c>act</c> claim) behind an impersonation token.</summary>
    public const string ImpersonatedBy = nameof(ImpersonatedBy);

    /// <summary>The property carrying the request's trace id.</summary>
    public const string TraceId = nameof(TraceId);

    /// <summary>
    /// Describes the caller as log properties. An anonymous or unauthenticated principal yields
    /// nothing, so a log event never claims an identity that was not established.
    /// </summary>
    /// <param name="principal">The request's principal.</param>
    /// <returns>
    /// <see cref="UserId"/> when the caller has an id, <see cref="IsImpersonated"/> for every
    /// authenticated caller, and <see cref="ImpersonatedBy"/> when the caller is impersonated and the
    /// actor is readable.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="principal"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<KeyValuePair<string, object?>> Describe(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (principal.Identity?.IsAuthenticated != true)
        {
            return [];
        }

        var properties = new List<KeyValuePair<string, object?>>(3);

        if (principal.FindFirst(ClaimTypes.NameIdentifier)?.Value is { Length: > 0 } userId)
        {
            properties.Add(new(UserId, userId));
        }

        var impersonated = principal.IsImpersonated();
        properties.Add(new(IsImpersonated, impersonated));

        if (impersonated && principal.GetActorId() is { Length: > 0 } actor)
        {
            properties.Add(new(ImpersonatedBy, actor));
        }

        return properties;
    }
}
