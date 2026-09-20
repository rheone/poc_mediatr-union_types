using System.Diagnostics;
using System.Text;
using System.Text.Json.Serialization;

namespace MediatrUnionPoc.Application.Features.Impersonation.IssueToken;

/// <summary>
/// The success case of <see cref="IssueImpersonationTokenResult"/>: a signed, short-lived bearer token
/// and the identity it acts as. The <see cref="Token"/> is a credential: it is deliberately absent from
/// <see cref="ToString"/> and the debugger display so it cannot leak through an incidental log line.
/// </summary>
/// <param name="Token">The compact serialized token to present as <c>Authorization: Bearer ...</c>.</param>
/// <param name="ExpiresAt">When the token stops being valid (UTC).</param>
/// <param name="UserId">The effective identity: the token's <c>sub</c>.</param>
/// <param name="Roles">The effective roles: the token's <c>role</c> claims.</param>
/// <param name="ActorId">The real caller the token was issued to, recorded in its <c>act</c> claim.</param>
[DebuggerDisplay("Impersonation of {UserId} by {ActorId} until {ExpiresAt}")]
public sealed record ImpersonationToken(
    string Token,
    DateTimeOffset ExpiresAt,
    string UserId,
    IReadOnlyList<string> Roles,
    string ActorId
)
{
    /// <summary>
    /// Gets the token's unique id (its <c>jti</c>), so the audit trail can tie every request made
    /// with the token back to the record of its issue. Not part of the response body and never the
    /// token itself.
    /// </summary>
    [JsonIgnore]
    public string? TokenId { get; init; }

    /// <summary>The scheme to present <see cref="Token"/> under; always <c>Bearer</c>.</summary>
    public string TokenType { get; init; } = "Bearer";

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("UserId = ").Append(UserId);
        builder.Append(", ActorId = ").Append(ActorId);
        builder.Append(", ExpiresAt = ").Append(ExpiresAt);
        return true;
    }
}
