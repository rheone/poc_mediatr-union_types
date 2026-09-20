using System.ComponentModel.DataAnnotations;

namespace MediatrUnionPoc.Api.Authentication;

/// <summary>
/// Settings for the JWT bearer authentication registered by
/// <see cref="AuthenticationServiceCollectionExtensions.AddJwtAuthentication"/>. Bound from the
/// <see cref="SectionName"/> configuration section and validated on start (by the source-generated
/// <see cref="JwtAuthOptionsValidator"/>), so a host without a usable signing key refuses to start
/// instead of accepting or rejecting every token at run time. Tokens are HMAC-SHA256 (HS256) signed.
/// </summary>
public sealed class JwtAuthOptions
{
    /// <summary>The configuration section the options bind from.</summary>
    public const string SectionName = "Authentication:Jwt";

    /// <summary>The fewest characters a <see cref="SigningKey"/> may have: 32 characters is 256 bits, the strength HS256 is designed for (the library itself rejects anything under 128 bits).</summary>
    public const int MinimumSigningKeyLength = 32;

    /// <summary>Gets or sets the <c>iss</c> a token must carry to be accepted.</summary>
    [Required]
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Gets or sets the <c>aud</c> a token must carry to be accepted.</summary>
    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the shared secret tokens are signed with (its UTF-8 bytes are the HMAC key), at
    /// least <see cref="MinimumSigningKeyLength"/> characters. No default: it must come from
    /// configuration, and only <c>appsettings.Development.json</c> ships one, clearly labelled as
    /// development-only. Supply it to any other environment through user secrets, an environment
    /// variable (<c>Authentication__Jwt__SigningKey</c>) or a secret store, never a committed file.
    /// </summary>
    [Required]
    [MinLength(MinimumSigningKeyLength)]
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Gets or sets how many seconds of clock difference between the issuer and this host an <c>exp</c>/<c>nbf</c> check tolerates. Defaults to 30 (the framework default is 300).</summary>
    [Range(0, 300)]
    public int ClockSkewSeconds { get; set; } = 30;
}
