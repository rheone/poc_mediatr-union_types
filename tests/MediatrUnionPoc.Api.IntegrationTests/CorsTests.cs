using System.Net;
using System.Net.Http.Json;
using MediatrUnionPoc.Api.Cors;
using MediatrUnionPoc.Api.IntegrationTests.TestData;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>
/// Exercises the CORS policy over real HTTP against the real pipeline, including the fallback
/// authorization policy: which origins are answered, that a preflight never needs credentials, and which
/// response headers a browser is allowed to read. The test host runs in Development, so the localhost
/// origins from <c>appsettings.Development.json</c> are configured unless a test replaces them.
/// </summary>
[Trait("Category", "Integration")]
public sealed class CorsTests : IDisposable
{
    private const string AppOrigin = "https://app.example.com";
    private const string EvilOrigin = "https://evil.example.com";
    private const string AllowOriginHeader = "Access-Control-Allow-Origin";
    private static readonly Guid AnyProductId = new("6f1c2b0e-4d5a-4b7e-9c31-0a8d2e5f7b14");

    private readonly ProductsApiFactory _factory = new();

    /// <inheritdoc/>
    public void Dispose() => _factory.Dispose();

    /// <summary>Verifies an allowed origin is echoed back exactly (never <c>*</c>) and the response varies by origin.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Request_AllowedOrigin_GetsExactOriginAndVaryOrigin_Test()
    {
        // Arrange
        using var client = ClientFor(o =>
            o.AllowedOrigins = [AppOrigin, "https://admin.example.com"]
        );
        using var request = CrossOriginGet(ApiRoutes.Products, AppOrigin);

        // Act
        using var response = await client.SendAsync(request, CancellationToken.None);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.OK, response.StatusCode),
            () => Assert.Equal(AppOrigin, Single(response, AllowOriginHeader)),
            () => Assert.Contains("Origin", response.Headers.Vary)
        );
    }

    /// <summary>Verifies an origin that is not on the list gets no CORS header, and the response still carries the trace id.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Request_DisallowedOrigin_GetsNoAllowOriginButStillATraceId_Test()
    {
        // Arrange
        using var client = ClientFor(o => o.AllowedOrigins = [AppOrigin]);
        using var request = CrossOriginGet(ApiRoutes.Products, EvilOrigin);

        // Act
        using var response = await client.SendAsync(request, CancellationToken.None);

        // Assert
        Assert.Multiple(
            () => Assert.False(response.Headers.Contains(AllowOriginHeader)),
            () => Assert.True(response.Headers.Contains("X-Trace-Id"))
        );
    }

    /// <summary>Verifies with no origins configured, an origin that looks legitimate gets no CORS header on a request or a preflight.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Request_NoOriginsConfigured_GetsNoCorsHeaders_Test()
    {
        // Arrange
        using var client = ClientFor(o => o.AllowedOrigins = []);
        using var request = CrossOriginGet(ApiRoutes.Products, AppOrigin);
        using var preflight = Preflight(ApiRoutes.Products, AppOrigin, "GET");

        // Act
        using var response = await client.SendAsync(request, CancellationToken.None);
        using var preflightResponse = await client.SendAsync(preflight, CancellationToken.None);

        // Assert
        Assert.Multiple(
            () => Assert.Empty(CorsHeaders(response)),
            () => Assert.Empty(CorsHeaders(preflightResponse))
        );
    }

    /// <summary>Verifies the localhost development origins ship in the Development configuration.</summary>
    /// <param name="origin">A localhost dev-server origin.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData("http://localhost:5173")]
    [InlineData("http://localhost:4200")]
    [InlineData("http://localhost:3000")]
    public async Task Request_DevelopmentConfiguration_AllowsLocalhostDevServers_Test(string origin)
    {
        // Arrange
        using var client = _factory.CreateClient().AsUser(ProductRequestMother.DefaultCallerId);
        using var request = CrossOriginGet(ApiRoutes.Products, origin);

        // Act
        using var response = await client.SendAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal(origin, Single(response, AllowOriginHeader));
    }

    /// <summary>Verifies a PATCH preflight (merge-patch content type plus If-Match) is granted with no credentials, past the fallback authorization policy, and carries the trace id.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Preflight_PatchWithMergePatchAndIfMatch_IsGrantedWithoutAuthentication_Test()
    {
        // Arrange
        using var client = ClientFor(o => o.AllowedOrigins = [AppOrigin], authenticated: false);
        using var preflight = Preflight(
            $"{ApiRoutes.Products}/{AnyProductId}",
            AppOrigin,
            "PATCH",
            "content-type,if-match"
        );
        using var control = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.Products);

        // Act
        using var response = await client.SendAsync(preflight, CancellationToken.None);
        using var unauthenticated = await client.SendAsync(control, CancellationToken.None);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.NoContent, response.StatusCode),
            () => Assert.Equal(AppOrigin, Single(response, AllowOriginHeader)),
            () => Assert.Contains("PATCH", Single(response, "Access-Control-Allow-Methods")),
            () =>
                Assert.Contains(
                    "If-Match",
                    Single(response, "Access-Control-Allow-Headers"),
                    StringComparison.OrdinalIgnoreCase
                ),
            () =>
                Assert.Contains(
                    "Content-Type",
                    Single(response, "Access-Control-Allow-Headers"),
                    StringComparison.OrdinalIgnoreCase
                ),
            () => Assert.Equal("600", Single(response, "Access-Control-Max-Age")),
            () => Assert.True(response.Headers.Contains("X-Trace-Id")),
            () => Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode)
        );
    }

    /// <summary>Verifies a preflight asking for a method outside the allowed list is not granted: the answer lists only the allowed methods, which the browser then refuses to proceed past.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Preflight_DisallowedMethod_IsNotGranted_Test()
    {
        // Arrange
        using var client = ClientFor(o =>
        {
            o.AllowedOrigins = [AppOrigin];
            o.AllowedMethods = ["GET"];
        });
        using var preflight = Preflight(ApiRoutes.Products, AppOrigin, "DELETE");

        // Act
        using var response = await client.SendAsync(preflight, CancellationToken.None);

        // Assert
        Assert.DoesNotContain("DELETE", Single(response, "Access-Control-Allow-Methods"));
    }

    /// <summary>Verifies a preflight asking for a header outside the allowed list is not granted: the answer lists only the allowed headers.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Preflight_DisallowedHeader_IsNotGranted_Test()
    {
        // Arrange
        using var client = ClientFor(o => o.AllowedOrigins = [AppOrigin]);
        using var preflight = Preflight(ApiRoutes.Products, AppOrigin, "GET", "x-evil");

        // Act
        using var response = await client.SendAsync(preflight, CancellationToken.None);

        // Assert
        Assert.DoesNotContain(
            "x-evil",
            Single(response, "Access-Control-Allow-Headers"),
            StringComparison.OrdinalIgnoreCase
        );
    }

    /// <summary>Verifies a preflight from an origin that is not allowed is not granted.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Preflight_DisallowedOrigin_IsNotGranted_Test()
    {
        // Arrange
        using var client = ClientFor(o => o.AllowedOrigins = [AppOrigin]);
        using var preflight = Preflight(ApiRoutes.Products, EvilOrigin, "GET");

        // Act
        using var response = await client.SendAsync(preflight, CancellationToken.None);

        // Assert
        Assert.Empty(CorsHeaders(response));
    }

    /// <summary>Verifies the exposed-headers list is sent on real cross-origin responses and every listed header that the API emits is really present where it is emitted.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Response_CrossOrigin_ExposesEveryListedHeaderTheApiEmits_Test()
    {
        // Arrange
        using var client = ClientFor(o => o.AllowedOrigins = [AppOrigin]);
        using var create = CrossOriginRequest(HttpMethod.Post, ApiRoutes.Products, AppOrigin);
        create.Content = JsonContent.Create(new { name = "Widget", price = 9.99m });

        // Act
        using var created = await client.SendAsync(create, CancellationToken.None);
        using var getOne = CrossOriginGet(created.Headers.Location!.ToString(), AppOrigin);
        using var one = await client.SendAsync(getOne, CancellationToken.None);
        using var getList = CrossOriginGet(ApiRoutes.Products, AppOrigin);
        using var list = await client.SendAsync(getList, CancellationToken.None);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.Created, created.StatusCode),
            () => Assert.True(created.Headers.Contains("Location")),
            () => Assert.True(one.Headers.Contains("ETag")),
            () => Assert.True(list.Headers.Contains("Link")),
            () => Assert.True(list.Headers.Contains("X-Total-Count")),
            () => Assert.True(list.Headers.Contains("X-Trace-Id")),
            () => Assert.True(list.Headers.Contains(ApiRoutes.SupportedVersionsHeader)),
            () => AssertExposesAllDefaults(created),
            () => AssertExposesAllDefaults(one),
            () => AssertExposesAllDefaults(list)
        );
    }

    /// <summary>Verifies credentials are not allowed unless configured.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Response_CredentialsNotConfigured_HasNoAllowCredentials_Test()
    {
        // Arrange
        using var client = ClientFor(o => o.AllowedOrigins = [AppOrigin]);
        using var request = CrossOriginGet(ApiRoutes.Products, AppOrigin);

        // Act
        using var response = await client.SendAsync(request, CancellationToken.None);

        // Assert
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    /// <summary>Verifies <c>AllowCredentials</c> adds <c>Access-Control-Allow-Credentials: true</c> alongside the exact origin.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Response_CredentialsConfigured_HasAllowCredentialsTrue_Test()
    {
        // Arrange
        using var client = ClientFor(o =>
        {
            o.AllowedOrigins = [AppOrigin];
            o.AllowCredentials = true;
        });
        using var request = CrossOriginGet(ApiRoutes.Products, AppOrigin);

        // Act
        using var response = await client.SendAsync(request, CancellationToken.None);

        // Assert
        Assert.Multiple(
            () => Assert.Equal("true", Single(response, "Access-Control-Allow-Credentials")),
            () => Assert.Equal(AppOrigin, Single(response, AllowOriginHeader))
        );
    }

    /// <summary>Verifies an authenticated cross-origin write works end to end: created, and readable back with the CORS header.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Request_AuthenticatedCrossOriginWrite_WorksEndToEnd_Test()
    {
        // Arrange
        using var client = ClientFor(o => o.AllowedOrigins = [AppOrigin]);
        using var create = CrossOriginRequest(HttpMethod.Post, ApiRoutes.Products, AppOrigin);
        create.Content = JsonContent.Create(new { name = "Gadget", price = 4.5m });

        // Act
        using var response = await client.SendAsync(create, CancellationToken.None);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.Created, response.StatusCode),
            () => Assert.Equal(AppOrigin, Single(response, AllowOriginHeader))
        );
    }

    /// <summary>Verifies configured lists replace the defaults rather than being appended to them by the configuration binder.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Configuration_ConfiguredMethods_ReplaceTheDefaults_Test()
    {
        // Arrange
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Cors:AllowedOrigins:0", AppOrigin);
            builder.UseSetting("Cors:AllowedMethods:0", "GET");
        });
        using var client = factory.CreateClient();
        using var allowed = Preflight(ApiRoutes.Products, AppOrigin, "GET");
        using var refused = Preflight(ApiRoutes.Products, AppOrigin, "DELETE");

        // Act
        using var allowedResponse = await client.SendAsync(allowed, CancellationToken.None);
        using var refusedResponse = await client.SendAsync(refused, CancellationToken.None);

        // Assert
        Assert.Multiple(
            () => Assert.Equal("GET", Single(allowedResponse, "Access-Control-Allow-Methods")),
            () =>
                Assert.DoesNotContain(
                    "DELETE",
                    Single(refusedResponse, "Access-Control-Allow-Methods")
                )
        );
    }

    private static void AssertExposesAllDefaults(HttpResponseMessage response)
    {
        var exposed = Single(response, "Access-Control-Expose-Headers")
            .Split(',', StringSplitOptions.TrimEntries);

        foreach (var header in ApiCorsOptions.DefaultExposedHeaders)
        {
            Assert.Contains(header, exposed, StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string Single(HttpResponseMessage response, string header) =>
        string.Join(',', response.Headers.GetValues(header));

    private static List<string> CorsHeaders(HttpResponseMessage response) =>
        [
            .. response
                .Headers.Select(h => h.Key)
                .Where(k => k.StartsWith("Access-Control-", StringComparison.OrdinalIgnoreCase)),
        ];

    private static HttpRequestMessage CrossOriginRequest(
        HttpMethod method,
        string uri,
        string origin
    )
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.TryAddWithoutValidation("Origin", origin);
        return request;
    }

    private static HttpRequestMessage CrossOriginGet(string uri, string origin) =>
        CrossOriginRequest(HttpMethod.Get, uri, origin);

    private static HttpRequestMessage Preflight(
        string uri,
        string origin,
        string method,
        string? requestHeaders = null
    )
    {
        var request = CrossOriginRequest(HttpMethod.Options, uri, origin);
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Method", method);
        if (requestHeaders is not null)
        {
            request.Headers.TryAddWithoutValidation(
                "Access-Control-Request-Headers",
                requestHeaders
            );
        }

        return request;
    }

    private HttpClient ClientFor(Action<ApiCorsOptions> configure, bool authenticated = true)
    {
        var client = _factory
            .WithWebHostBuilder(builder =>
                builder.ConfigureTestServices(services => services.PostConfigure(configure))
            )
            .CreateClient();

        return authenticated ? client.AsUser(ProductRequestMother.DefaultCallerId) : client;
    }
}
