namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>Lets a test state its caller identity for the <see cref="TestAuthenticationHandler"/> scheme.</summary>
public static class TestIdentityExtensions
{
    /// <summary>
    /// Makes every request the client sends come from the given user. A per-request
    /// <see cref="AsUser(HttpRequestMessage, string?, string[])"/> takes precedence over this default.
    /// </summary>
    /// <param name="client">The client to configure.</param>
    /// <param name="userId">The caller's id (the <c>NameIdentifier</c> claim); <see langword="null"/> or empty for an authenticated caller with no subject.</param>
    /// <param name="roles">The roles the caller holds.</param>
    /// <returns><paramref name="client"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> or <paramref name="roles"/> is <see langword="null"/>.</exception>
    public static HttpClient AsUser(this HttpClient client, string? userId, params string[] roles)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(roles);

        client.DefaultRequestHeaders.Remove(TestAuthenticationHandler.AuthenticatedHeaderName);
        client.DefaultRequestHeaders.Remove(TestAuthenticationHandler.UserHeaderName);
        client.DefaultRequestHeaders.Remove(TestAuthenticationHandler.RolesHeaderName);
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            TestAuthenticationHandler.AuthenticatedHeaderName,
            "true"
        );
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            TestAuthenticationHandler.UserHeaderName,
            string.IsNullOrEmpty(userId) ? TestAuthenticationHandler.NoSubject : userId
        );
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            TestAuthenticationHandler.RolesHeaderName,
            "," + string.Join(',', roles)
        );

        return client;
    }

    /// <summary>Makes this one request come from the given user, overriding any identity set on the client.</summary>
    /// <param name="request">The request to configure.</param>
    /// <param name="userId">The caller's id (the <c>NameIdentifier</c> claim); <see langword="null"/> or empty for an authenticated caller with no subject.</param>
    /// <param name="roles">The roles the caller holds.</param>
    /// <returns><paramref name="request"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> or <paramref name="roles"/> is <see langword="null"/>.</exception>
    public static HttpRequestMessage AsUser(
        this HttpRequestMessage request,
        string? userId,
        params string[] roles
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(roles);

        request.Headers.Remove(TestAuthenticationHandler.AuthenticatedHeaderName);
        request.Headers.Remove(TestAuthenticationHandler.UserHeaderName);
        request.Headers.Remove(TestAuthenticationHandler.RolesHeaderName);
        request.Headers.TryAddWithoutValidation(
            TestAuthenticationHandler.AuthenticatedHeaderName,
            "true"
        );
        request.Headers.TryAddWithoutValidation(
            TestAuthenticationHandler.UserHeaderName,
            string.IsNullOrEmpty(userId) ? TestAuthenticationHandler.NoSubject : userId
        );
        request.Headers.TryAddWithoutValidation(
            TestAuthenticationHandler.RolesHeaderName,
            "," + string.Join(',', roles)
        );

        return request;
    }
}
