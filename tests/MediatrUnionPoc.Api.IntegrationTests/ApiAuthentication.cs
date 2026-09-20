namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>Which authentication scheme a <see cref="ProductsApiFactory"/> host answers requests with.</summary>
public enum ApiAuthentication
{
    /// <summary>The header-driven <see cref="TestAuthenticationHandler"/> is the default scheme; tests state their identity with <c>AsUser</c>.</summary>
    TestScheme,

    /// <summary>The production JWT bearer scheme is left as the default; tests present real signed tokens.</summary>
    RealJwt,
}
