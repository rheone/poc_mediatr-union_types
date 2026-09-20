using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace MediatrUnionPoc.Api.Authentication;

/// <summary>
/// Configures the <see cref="JwtBearerDefaults.AuthenticationScheme"/> handler from
/// <see cref="JwtAuthOptions"/>: issuer, audience, signing key, lifetime and signature validation
/// all on, HS256 only, and inbound claim mapping switched on deliberately.
/// </summary>
/// <remarks>
/// <para>
/// <b>Claim mapping.</b> <see cref="JwtBearerOptions.MapInboundClaims"/> is set to
/// <see langword="true"/> explicitly (it is also the framework default, which the tests assert). The
/// Application layer authorizes against <see cref="System.Security.Claims.ClaimTypes.NameIdentifier"/>
/// and <see cref="System.Security.Claims.ClaimTypes.Role"/>; with mapping on, a token's standard
/// <c>sub</c> and <c>role</c> claims arrive under exactly those types, so the two agree without any
/// change to the Application layer. Setting it to <see langword="false"/> would leave them as
/// <c>sub</c>/<c>role</c> and silently make every caller ownerless and role-less.
/// </para>
/// <para>
/// <b>Additive keys.</b> The signing key, issuer and audience are appended to whatever the built-in
/// <c>Authentication:Schemes:Bearer</c> configuration binding already put on the validation
/// parameters (that is where <c>dotnet user-jwts</c> writes its settings), never replacing it. In
/// Development a <c>dotnet user-jwts</c> token is therefore accepted alongside tokens signed with
/// <see cref="JwtAuthOptions.SigningKey"/>; a host with no such configuration accepts only the latter.
/// </para>
/// </remarks>
/// <param name="options">The validated JWT settings.</param>
/// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
public sealed class ConfigureJwtBearerOptions(IOptions<JwtAuthOptions> options)
    : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly JwtAuthOptions _options = (
        options ?? throw new ArgumentNullException(nameof(options))
    ).Value;

    /// <inheritdoc/>
    public void Configure(JwtBearerOptions options) =>
        Configure(JwtBearerDefaults.AuthenticationScheme, options);

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public void Configure(string? name, JwtBearerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (name != JwtBearerDefaults.AuthenticationScheme)
        {
            return;
        }

        options.MapInboundClaims = true;

        var parameters = options.TokenValidationParameters;
        parameters.ValidateIssuer = true;
        parameters.ValidateAudience = true;
        parameters.ValidateLifetime = true;
        parameters.ValidateIssuerSigningKey = true;
        parameters.RequireExpirationTime = true;
        parameters.RequireSignedTokens = true;
        parameters.ValidAlgorithms = [SecurityAlgorithms.HmacSha256];
        parameters.ClockSkew = TimeSpan.FromSeconds(_options.ClockSkewSeconds);
        parameters.ValidIssuers = (parameters.ValidIssuers ?? []).Append(_options.Issuer);
        parameters.ValidAudiences = (parameters.ValidAudiences ?? []).Append(_options.Audience);
        parameters.IssuerSigningKeys = (parameters.IssuerSigningKeys ?? []).Append(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey))
        );
    }
}
