using System.Text;
using MediatrUnionPoc.Api.Authentication;
using MediatrUnionPoc.Application.Common.Authorization;
using MediatrUnionPoc.Application.Features.Impersonation.IssueToken;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace MediatrUnionPoc.Api.Impersonation;

/// <summary>
/// Builds and signs impersonation tokens as HS256 JWTs with <see cref="ImpersonationOptions.SigningKey"/>,
/// the same issuer and audience as ordinary tokens. Claims: <c>sub</c> (the target), <c>role</c>
/// (one per granted role), <c>act</c> (RFC 8693: <c>{"sub":"&lt;real caller&gt;"}</c> as a nested JSON object),
/// <c>impersonated</c> (<see langword="true"/>), <c>imp_reason</c>, <c>imp_ticket</c> (when given),
/// <c>jti</c>, and <c>iat</c>/<c>nbf</c>/<c>exp</c> at whole-second precision (the returned expiry is
/// exactly the token's <c>exp</c>).
/// </summary>
/// <param name="jwt">Supplies the issuer and audience.</param>
/// <param name="impersonation">Supplies the signing key.</param>
/// <param name="clock">The clock the lifetime is measured from.</param>
/// <exception cref="ArgumentNullException"><paramref name="jwt"/>, <paramref name="impersonation"/> or <paramref name="clock"/> is <see langword="null"/>.</exception>
public sealed class JwtImpersonationTokenIssuer(
    IOptions<JwtAuthOptions> jwt,
    IOptions<ImpersonationOptions> impersonation,
    TimeProvider clock
) : IImpersonationTokenIssuer
{
    private readonly JwtAuthOptions _jwt = (
        jwt ?? throw new ArgumentNullException(nameof(jwt))
    ).Value;

    private readonly ImpersonationOptions _impersonation = (
        impersonation ?? throw new ArgumentNullException(nameof(impersonation))
    ).Value;

    private readonly TimeProvider _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    private readonly JsonWebTokenHandler _handler = new();

    private SigningCredentials? _credentials;

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="grant"/> is <see langword="null"/>.</exception>
    public ImpersonationToken Issue(ImpersonationGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);

        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(_clock.GetUtcNow().ToUnixTimeSeconds());
        var expiresAt = issuedAt + grant.Lifetime;

        var tokenId = Guid.NewGuid().ToString("N");
        var claims = new Dictionary<string, object>
        {
            ["sub"] = grant.TargetUserId,
            [ImpersonationClaims.Actor] = new Dictionary<string, object>
            {
                ["sub"] = grant.ActorId,
            },
            [ImpersonationClaims.Impersonated] = true,
            [ImpersonationClaims.Reason] = grant.Reason,
            [ImpersonationClaims.TokenId] = tokenId,
        };

        if (grant.Roles.Count > 0)
        {
            claims["role"] = grant.Roles.ToArray();
        }

        if (grant.TicketReference is not null)
        {
            claims[ImpersonationClaims.Ticket] = grant.TicketReference;
        }

        var token = _handler.CreateToken(
            new SecurityTokenDescriptor
            {
                Issuer = _jwt.Issuer,
                Audience = _jwt.Audience,
                Claims = claims,
                IssuedAt = issuedAt.UtcDateTime,
                NotBefore = issuedAt.UtcDateTime,
                Expires = expiresAt.UtcDateTime,
                SigningCredentials = Credentials(),
            }
        );

        return new ImpersonationToken(
            token,
            expiresAt,
            grant.TargetUserId,
            grant.Roles,
            grant.ActorId
        )
        {
            TokenId = tokenId,
        };
    }

    // Built on first use: a host with impersonation switched off has no key and never gets here.
    private SigningCredentials Credentials() =>
        _credentials ??= new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_impersonation.SigningKey)),
            SecurityAlgorithms.HmacSha256
        );
}
