using System.Diagnostics.CodeAnalysis;

namespace MediatrUnionPoc.Api.Proxies;

/// <summary>
/// Which reverse proxies the API trusts to say who the real client is, registered by
/// <see cref="ForwardedHeadersServiceCollectionExtensions.AddApiForwardedHeaders"/>. Bound from the
/// <see cref="SectionName"/> configuration section and validated on start (by
/// <see cref="ApiForwardedHeadersOptionsValidator"/>). Secure by default: with no trusted proxy the
/// forwarded-headers middleware is not enabled at all and every <c>X-Forwarded-*</c> header a client
/// sends is ignored, so a client cannot choose the address it is rate limited (or logged and audited) under.
/// The name avoids the framework's own <c>Microsoft.AspNetCore.Builder.ForwardedHeadersOptions</c>.
/// </summary>
public sealed class ApiForwardedHeadersOptions
{
    /// <summary>The configuration section the options bind from.</summary>
    public const string SectionName = "ForwardedHeaders";

    /// <summary>
    /// Gets or sets the proxies whose <c>X-Forwarded-For</c> and <c>X-Forwarded-Proto</c> headers are
    /// honoured, each a single IP address (<c>10.0.0.5</c>) or a CIDR network (<c>10.0.0.0/24</c>). A
    /// request from any other address keeps its own connection address. Empty by default: the
    /// middleware is off. The catch-all networks (<c>0.0.0.0/0</c>, <c>::/0</c>) and the unspecified
    /// addresses are rejected, since trusting everyone is the same as trusting the client.
    /// </summary>
    [SuppressMessage(
        "Performance",
        "CA1819:Properties should not return arrays",
        Justification = "Configuration binding target; a settable array binds from a JSON array."
    )]
    public string[] TrustedProxies { get; set; } = [];
}
