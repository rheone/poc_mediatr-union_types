using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>
/// Test-only authentication scheme that reads the caller's identity from request headers, so a test
/// states who is calling without minting a token. A request without <see cref="AuthenticatedHeaderName"/> is
/// anonymous (no result, so the host's fallback policy answers 401); with it the caller is
/// authenticated, holding a <see cref="ClaimTypes.NameIdentifier"/> claim of <see cref="UserHeaderName"/>'s
/// value (none when that value is <see cref="NoSubject"/>: authenticated, but no subject) and one
/// <see cref="ClaimTypes.Role"/> claim per comma-separated entry of <see cref="RolesHeaderName"/>. Set the
/// headers through <c>AsUser</c> (<see cref="TestIdentityExtensions"/>) rather than by hand.
/// </summary>
/// <param name="options">The scheme options monitor.</param>
/// <param name="logger">The logger factory.</param>
/// <param name="encoder">The URL encoder.</param>
public sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>The name the scheme is registered under.</summary>
    public const string SchemeName = "Test";

    /// <summary>The header whose presence authenticates the request.</summary>
    public const string AuthenticatedHeaderName = "X-Test-Authenticated";

    /// <summary>The header whose value is the caller's id.</summary>
    public const string UserHeaderName = "X-Test-User";

    /// <summary>The <see cref="UserHeaderName"/> value meaning "authenticated, but the token has no subject". A header cannot be sent empty, and it must be sent to override a client-wide default user.</summary>
    public const string NoSubject = "<no-subject>";

    /// <summary>The header carrying the caller's roles, comma separated.</summary>
    public const string RolesHeaderName = "X-Test-Roles";

    /// <inheritdoc/>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey(AuthenticatedHeaderName))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        List<Claim> claims = [];
        var userId = Request.Headers[UserHeaderName].ToString();
        if (userId.Length > 0 && userId != NoSubject)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
        }

        foreach (
            var role in Request
                .Headers[RolesHeaderName]
                .ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        )
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName)),
            SchemeName
        );
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
