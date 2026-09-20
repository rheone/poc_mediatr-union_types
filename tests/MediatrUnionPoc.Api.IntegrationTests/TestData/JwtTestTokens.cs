using System.Text;
using MediatrUnionPoc.Api.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace MediatrUnionPoc.Api.IntegrationTests.TestData;

/// <summary>Mints real signed JWTs for the tests that exercise the production bearer scheme, using the issuer, audience and (Development) key the host itself is configured with.</summary>
public static class JwtTestTokens
{
    /// <summary>A valid-length signing key for tests that run a non-Development host, which has no key of its own configured.</summary>
    public const string NonDevelopmentSigningKey =
        "test-only-signing-key-for-non-development-hosts";

    /// <summary>Creates a signed token.</summary>
    /// <param name="factory">The host whose configured <see cref="JwtAuthOptions"/> the token is minted for.</param>
    /// <param name="subject">The <c>sub</c> claim, or <see langword="null"/> for a token with none.</param>
    /// <param name="roles">The values of the <c>role</c> claim (one value is written as a string, several as an array, none omits the claim).</param>
    /// <param name="lifetime">How long from now the token is valid; negative for an already expired token (its <c>nbf</c> is then an hour before its <c>exp</c>).</param>
    /// <param name="signingKey">Signs with this key instead of the host's, to make a token whose signature does not verify.</param>
    /// <param name="audience">Overrides the <c>aud</c> claim.</param>
    /// <returns>The compact serialized token.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
    public static string Create(
        ProductsApiFactory factory,
        string? subject,
        string[]? roles = null,
        TimeSpan? lifetime = null,
        string? signingKey = null,
        string? audience = null
    )
    {
        ArgumentNullException.ThrowIfNull(factory);

        var options = factory.Services.GetRequiredService<IOptions<JwtAuthOptions>>().Value;
        var now = DateTime.UtcNow;
        var expires = now + (lifetime ?? TimeSpan.FromMinutes(30));
        var claims = new Dictionary<string, object>();
        if (subject is not null)
        {
            claims["sub"] = subject;
        }

        if (roles is { Length: 1 })
        {
            claims["role"] = roles[0];
        }
        else if (roles is { Length: > 1 })
        {
            claims["role"] = roles;
        }

        return new JsonWebTokenHandler().CreateToken(
            new SecurityTokenDescriptor
            {
                Issuer = options.Issuer,
                Audience = audience ?? options.Audience,
                Claims = claims,
                NotBefore = expires < now ? expires.AddHours(-1) : now,
                Expires = expires,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(signingKey ?? options.SigningKey)
                    ),
                    SecurityAlgorithms.HmacSha256
                ),
            }
        );
    }
}
