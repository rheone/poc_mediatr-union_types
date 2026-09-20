namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>
/// The one place the integration tests spell the API's URLs. The versioned routes are what every
/// ordinary test uses; the <c>Unversioned</c> members exist only for the tests that deliberately
/// exercise the transitional alias (<c>ApiVersioningTests</c>).
/// </summary>
internal static class ApiRoutes
{
    /// <summary>The versioned products collection route (also the create and list route).</summary>
    public const string Products = "/api/v1/products";

    /// <summary>The versioned impersonation token route.</summary>
    public const string ImpersonationTokens = "/api/v1/impersonation/tokens";

    /// <summary>The OpenAPI document of API version 1.</summary>
    public const string OpenApiV1 = "/openapi/v1.json";

    /// <summary>The path template of a single product as the OpenAPI document spells it.</summary>
    public const string ProductByIdTemplate = Products + "/{id}";

    /// <summary>The unversioned products alias of <see cref="Products"/>.</summary>
    public const string UnversionedProducts = "/api/products";

    /// <summary>The unversioned alias of <see cref="ImpersonationTokens"/>.</summary>
    public const string UnversionedImpersonationTokens = "/api/impersonation/tokens";

    /// <summary>The header that reports the versions the API supports.</summary>
    public const string SupportedVersionsHeader = "api-supported-versions";
}
