namespace MediatrUnionPoc.Application.Features.Impersonation.IssueToken;

/// <summary>
/// Signs an impersonation token. An abstraction so the Application layer decides <em>whether</em> a
/// token may be issued while the host, which owns the signing key and the token format, decides how
/// it is built; this layer depends on no JWT library.
/// </summary>
public interface IImpersonationTokenIssuer
{
    /// <summary>Builds and signs the token for an already-approved <paramref name="grant"/>.</summary>
    /// <param name="grant">What to grant, to whom, on whose behalf and why.</param>
    /// <returns>The signed token with its expiry and effective identity.</returns>
    ImpersonationToken Issue(ImpersonationGrant grant);
}

/// <summary>An approved request to mint an impersonation token.</summary>
/// <param name="ActorId">The id of the real caller, recorded in the token's actor claim.</param>
/// <param name="TargetUserId">The identity the token acts as (its <c>sub</c>).</param>
/// <param name="Roles">The roles the token carries; possibly empty.</param>
/// <param name="Reason">The recorded reason.</param>
/// <param name="TicketReference">The optional ticket the work is tracked under.</param>
/// <param name="Lifetime">How long the token is valid, from now.</param>
public sealed record ImpersonationGrant(
    string ActorId,
    string TargetUserId,
    IReadOnlyList<string> Roles,
    string Reason,
    string? TicketReference,
    TimeSpan Lifetime
);
