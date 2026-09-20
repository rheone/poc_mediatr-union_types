using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace MediatrUnionPoc.Api.Cors;

/// <summary>
/// Settings for the cross-origin resource sharing (CORS) policy registered by
/// <see cref="CorsServiceCollectionExtensions.AddApiCors"/>. Bound from the <see cref="SectionName"/>
/// configuration section and validated on start (by the source-generated
/// <see cref="ApiCorsOptionsValidator"/>), so a malformed origin stops the host from starting instead of
/// silently never matching. Secure by default: with no configuration <see cref="AllowedOrigins"/> is
/// empty and no origin is allowed, so no CORS header is ever sent. The name avoids the framework's own
/// <c>Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions</c>.
/// </summary>
/// <remarks>
/// The method, header and exposed-header lists are nullable so that "not configured" is distinguishable:
/// the configuration binder appends configured array elements to an array that already holds defaults,
/// which would turn a configured <c>["GET"]</c> into the defaults plus <c>GET</c>. Read the effective
/// values through <see cref="GetAllowedMethods"/>, <see cref="GetAllowedHeaders"/> and
/// <see cref="GetExposedHeaders"/>.
/// </remarks>
public sealed class ApiCorsOptions
{
    /// <summary>The configuration section the options bind from.</summary>
    public const string SectionName = "Cors";

    /// <summary>The smallest <see cref="PreflightMaxAgeSeconds"/>; zero tells browsers not to cache a preflight.</summary>
    public const int MinimumPreflightMaxAgeSeconds = 0;

    /// <summary>The largest <see cref="PreflightMaxAgeSeconds"/>, one day (browsers cap it lower anyway: Chromium at 2 hours, Firefox at 24).</summary>
    public const int MaximumPreflightMaxAgeSeconds = 86400;

    /// <summary>Gets the methods allowed when none are configured.</summary>
    public static IReadOnlyList<string> DefaultAllowedMethods { get; } =
    ["GET", "POST", "PUT", "PATCH", "DELETE"];

    /// <summary>
    /// Gets the request headers allowed when none are configured: the credential, the body type
    /// (<c>application/json</c> and <c>application/merge-patch+json</c> are not CORS-safelisted, so
    /// <c>Content-Type</c> forces a preflight), the concurrency precondition and content negotiation.
    /// </summary>
    public static IReadOnlyList<string> DefaultAllowedHeaders { get; } =
    ["Authorization", "Content-Type", "If-Match", "Accept"];

    /// <summary>
    /// Gets the response headers exposed to browser code when none are configured. A browser hides every
    /// response header that is not CORS-safelisted unless it is listed here, so this list is the contract
    /// with a browser client: the README table is checked against it by a test.
    /// </summary>
    public static IReadOnlyList<string> DefaultExposedHeaders { get; } =
    [
        "ETag",
        "Link",
        "X-Total-Count",
        "X-Trace-Id",
        "Location",
        "Retry-After",
        "api-supported-versions",
    ];

    /// <summary>
    /// Gets or sets the origins allowed to call the API from a browser, each written exactly as a browser
    /// sends it: <c>scheme://host[:port]</c>, lowercase, no path, query or trailing slash. There is no
    /// wildcard: <c>*</c> is rejected, and an origin is always listed explicitly. Defaults to empty, which
    /// allows nothing.
    /// </summary>
    [CorsOriginList]
    [SuppressMessage(
        "Performance",
        "CA1819:Properties should not return arrays",
        Justification = "Configuration binding target; a settable array binds from a JSON array."
    )]
    public string[] AllowedOrigins { get; set; } = [];

    /// <summary>Gets or sets the methods a preflight may ask for. <see langword="null"/> means <see cref="DefaultAllowedMethods"/>; when set it must hold at least one method token.</summary>
    [CorsTokenList]
    [SuppressMessage(
        "Performance",
        "CA1819:Properties should not return arrays",
        Justification = "Configuration binding target; a settable array binds from a JSON array."
    )]
    public string[]? AllowedMethods { get; set; }

    /// <summary>Gets or sets the request headers a preflight may ask for. <see langword="null"/> means <see cref="DefaultAllowedHeaders"/>; when set it must hold at least one header name.</summary>
    [CorsTokenList]
    [SuppressMessage(
        "Performance",
        "CA1819:Properties should not return arrays",
        Justification = "Configuration binding target; a settable array binds from a JSON array."
    )]
    public string[]? AllowedHeaders { get; set; }

    /// <summary>Gets or sets the response headers browser code may read. <see langword="null"/> means <see cref="DefaultExposedHeaders"/>; an empty array exposes none.</summary>
    [CorsTokenList(AllowEmpty = true)]
    [SuppressMessage(
        "Performance",
        "CA1819:Properties should not return arrays",
        Justification = "Configuration binding target; a settable array binds from a JSON array."
    )]
    public string[]? ExposedHeaders { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the API lets browser code send credentials (cookies, HTTP
    /// authentication) with a cross-origin request. The API authenticates with a bearer token that
    /// scripts attach themselves, so this stays off unless a cookie-based client is added. Defaults to
    /// <see langword="false"/>.
    /// </summary>
    public bool AllowCredentials { get; set; }

    /// <summary>Gets or sets how many seconds a browser may cache a preflight answer. Defaults to 600.</summary>
    [Range(MinimumPreflightMaxAgeSeconds, MaximumPreflightMaxAgeSeconds)]
    public int PreflightMaxAgeSeconds { get; set; } = 600;

    /// <summary>Gets the methods to allow: the configured ones, or <see cref="DefaultAllowedMethods"/>.</summary>
    /// <returns>The effective allowed methods.</returns>
    public IReadOnlyList<string> GetAllowedMethods() => AllowedMethods ?? DefaultAllowedMethods;

    /// <summary>Gets the request headers to allow: the configured ones, or <see cref="DefaultAllowedHeaders"/>.</summary>
    /// <returns>The effective allowed headers.</returns>
    public IReadOnlyList<string> GetAllowedHeaders() => AllowedHeaders ?? DefaultAllowedHeaders;

    /// <summary>Gets the response headers to expose: the configured ones, or <see cref="DefaultExposedHeaders"/>.</summary>
    /// <returns>The effective exposed headers.</returns>
    public IReadOnlyList<string> GetExposedHeaders() => ExposedHeaders ?? DefaultExposedHeaders;
}
