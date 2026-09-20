using System.Security.Claims;
using System.Text.Json;

namespace MediatrUnionPoc.Application.Common.Authorization;

/// <summary>
/// The claim names an impersonation token carries, and the readers that answer "is this caller
/// acting as someone else, and who really is it?" from a <see cref="ClaimsPrincipal"/>. Kept in the
/// Application layer because the impersonation handler itself must refuse a chained request; the Api
/// signs the claims into the token and the audit and logging steps read them back through here.
/// </summary>
/// <remarks>
/// <para>
/// None of these names is touched by the JWT handler's inbound claim mapping (which only renames
/// well-known JWT claims such as <c>sub</c> and <c>role</c>), so they reach the principal exactly as
/// written into the token.
/// </para>
/// <para>
/// <c>act</c> is the RFC 8693 actor claim: a JSON object naming the real caller,
/// <c>{"sub":"&lt;caller id&gt;"}</c>. On the principal it is a single claim of type <c>act</c> whose value is
/// that JSON text.
/// </para>
/// </remarks>
public static class ImpersonationClaims
{
    /// <summary>The RFC 8693 actor claim (<c>act</c>): a JSON object whose <c>sub</c> member is the real caller's id.</summary>
    public const string Actor = "act";

    /// <summary>The marker claim (<c>impersonated</c>, value <c>true</c>) present on every impersonation token.</summary>
    public const string Impersonated = "impersonated";

    /// <summary>The claim (<c>imp_reason</c>) carrying the recorded reason the token was issued.</summary>
    public const string Reason = "imp_reason";

    /// <summary>The claim (<c>imp_ticket</c>) carrying the optional ticket reference the token was issued under.</summary>
    public const string Ticket = "imp_ticket";

    /// <summary>The registered <c>jti</c> claim: the unique id of the token, recorded by the audit trail when the token is minted and on every request made with it.</summary>
    public const string TokenId = "jti";

    private const string ActorSubjectMember = "sub";

    extension(ClaimsPrincipal principal)
    {
        /// <summary>
        /// Gets whether the caller is using an impersonation token: it carries the <see cref="Impersonated"/>
        /// marker or an <see cref="Actor"/> claim. Either alone is enough, so a token that lost one of
        /// them is still recognised.
        /// </summary>
        /// <returns><see langword="true"/> when the principal is an impersonated identity.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="principal"/> is <see langword="null"/>.</exception>
        public bool IsImpersonated()
        {
            ArgumentNullException.ThrowIfNull(principal);

            return principal.HasClaim(claim =>
                    claim.Type == Impersonated
                    && string.Equals(claim.Value, "true", StringComparison.OrdinalIgnoreCase)
                ) || principal.HasClaim(claim => claim.Type == Actor);
        }

        /// <summary>Gets the <see cref="TokenId"/> (<c>jti</c>) of the token the caller presented.</summary>
        /// <returns>The token id, or <see langword="null"/> when the principal has none.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="principal"/> is <see langword="null"/>.</exception>
        public string? GetTokenId()
        {
            ArgumentNullException.ThrowIfNull(principal);

            var value = principal.FindFirst(TokenId)?.Value;
            return string.IsNullOrEmpty(value) ? null : value;
        }

        /// <summary>Gets the id of the real caller behind an impersonation token: the <c>sub</c> member of its <see cref="Actor"/> claim.</summary>
        /// <returns>The actor's id, or <see langword="null"/> when the principal has no readable actor claim.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="principal"/> is <see langword="null"/>.</exception>
        public string? GetActorId()
        {
            ArgumentNullException.ThrowIfNull(principal);

            var value = principal.FindFirst(Actor)?.Value;
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(value);
                return
                    document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty(ActorSubjectMember, out var subject)
                    && subject.ValueKind == JsonValueKind.String
                    ? subject.GetString()
                    : null;
            }
            catch (JsonException)
            {
                // A signed token never carries malformed JSON here; a hand-built principal might.
                return null;
            }
        }
    }
}
