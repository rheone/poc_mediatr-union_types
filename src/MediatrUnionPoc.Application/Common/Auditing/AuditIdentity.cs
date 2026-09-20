using System.Security.Claims;
using MediatrUnionPoc.Application.Common.Authorization;

namespace MediatrUnionPoc.Application.Common.Auditing;

/// <summary>
/// Who an audited request is from: the real caller, the identity it ran as, and, when the caller
/// used an impersonation token, that token's id. Derived from the principal alone.
/// </summary>
/// <param name="ActorId">The real caller: the <c>act</c> subject when impersonated (<see langword="null"/> when that claim is unreadable), else the caller's id.</param>
/// <param name="EffectiveId">The identity the request ran as: the caller's <c>NameIdentifier</c>.</param>
/// <param name="IsImpersonated">Whether the principal carries an impersonation token.</param>
/// <param name="TokenId">The token's <c>jti</c> when impersonated and present.</param>
public sealed record AuditIdentity(
    string? ActorId,
    string? EffectiveId,
    bool IsImpersonated,
    string? TokenId
)
{
    /// <summary>Gets the identity of a request with no principal.</summary>
    public static AuditIdentity Unknown { get; } = new(null, null, false, null);

    /// <summary>Derives the identity from <paramref name="principal"/>.</summary>
    /// <param name="principal">The request's principal; <see langword="null"/> yields <see cref="Unknown"/>.</param>
    /// <returns>The identity.</returns>
    public static AuditIdentity From(ClaimsPrincipal? principal)
    {
        if (principal is null)
        {
            return Unknown;
        }

        var effective = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!principal.IsImpersonated())
        {
            return new AuditIdentity(effective, effective, false, null);
        }

        return new AuditIdentity(principal.GetActorId(), effective, true, principal.GetTokenId());
    }
}
