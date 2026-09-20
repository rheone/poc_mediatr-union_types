namespace MediatrUnionPoc.Api.Http;

/// <summary>
/// The API versions this host serves and the route templates that carry them: the one place a
/// version number or a versioned path is spelled, so adding a version does not mean editing string
/// literals across controllers.
/// </summary>
public static class ApiVersions
{
    /// <summary>The version 1.0 declaration, as <c>[ApiVersion]</c> takes it.</summary>
    public const string V1 = "1.0";

    /// <summary>The value the <c>{version:apiVersion}</c> route segment takes for <see cref="V1"/> in a URL this API generates (<c>/api/v1/...</c>).</summary>
    public const string V1Segment = "1";

    /// <summary>The OpenAPI document (and ApiExplorer group) name of version 1.0; also the <c>/openapi/{name}.json</c> path segment.</summary>
    public const string V1DocumentName = "v1";

    /// <summary>The route prefix that carries the version: <c>api/v{version:apiVersion}</c>.</summary>
    public const string VersionedPrefix = "api/v{version:apiVersion}";

    /// <summary>
    /// The unversioned route prefix (<c>api</c>) kept as an alias for <see cref="V1"/> while existing
    /// callers move to the versioned URLs. Transitional: remove every <c>[Route]</c> that uses it (and
    /// <c>AssumeDefaultVersionWhenUnspecified</c>) to retire the unversioned URLs.
    /// </summary>
    public const string UnversionedAliasPrefix = "api";
}
