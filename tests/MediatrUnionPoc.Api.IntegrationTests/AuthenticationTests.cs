using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MediatrUnionPoc.Api.Authentication;
using MediatrUnionPoc.Api.IntegrationTests.TestData;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>
/// Exercises the secure-by-default behaviour of the host: with no credentials every product endpoint
/// answers 401 as a problem body, the health endpoints and (Development) OpenAPI document stay
/// anonymous, and the test scheme still drives the 403 paths. Runs on the header-driven test scheme;
/// the real JWT bearer scheme has its own tests in <see cref="JwtBearerAuthenticationTests"/>.
/// </summary>
[Trait("Category", "Integration")]
public sealed class AuthenticationTests : IDisposable
{
    private const string ProductsUri = ApiRoutes.Products;
    private const string ProblemJson = "application/problem+json";
    private const string TraceHeader = "X-Trace-Id";

    private readonly ProductsApiFactory _factory = new();

    /// <summary>Gets the verbs and URIs for <see cref="Request_NoCredentials_Returns401ProblemWithTraceId_Test"/>: every product endpoint, including the reads.</summary>
    public static TheoryData<
        string,
        string
    > Request_NoCredentials_Returns401ProblemWithTraceId_Test_Data =>
        new()
        {
            { "POST", ProductsUri },
            { "GET", $"{ProductsUri}/{ProductRequestMother.UnknownId}" },
            { "GET", ProductsUri },
            { "PUT", $"{ProductsUri}/{ProductRequestMother.UnknownId}" },
            { "PATCH", $"{ProductsUri}/{ProductRequestMother.UnknownId}" },
            { "DELETE", $"{ProductsUri}/{ProductRequestMother.UnknownId}" },
        };

    /// <summary>Disposes the test's backing <see cref="ProductsApiFactory"/>.</summary>
    public void Dispose() => _factory.Dispose();

    /// <summary>Verifies a request without credentials is refused with 401 on every verb, as an <c>application/problem+json</c> body whose <c>traceId</c> matches the <c>X-Trace-Id</c> header.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="uri">The request URI.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [MemberData(nameof(Request_NoCredentials_Returns401ProblemWithTraceId_Test_Data))]
    public async Task Request_NoCredentials_Returns401ProblemWithTraceId_Test(
        string method,
        string uri
    )
    {
        // Arrange
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), uri);
        if (method is "POST" or "PUT")
        {
            request.Content = JsonContent.Create(ProductRequestMother.Widget());
        }

        // Act
        using var response = await client.SendAsync(request, CancellationToken.None);

        // Assert
        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(CancellationToken.None)
        );
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode),
            () => Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType),
            () => Assert.Equal(401, body.RootElement.GetProperty("status").GetInt32()),
            () =>
                Assert.Equal(
                    response.Headers.GetValues(TraceHeader).Single(),
                    body.RootElement.GetProperty("traceId").GetString()
                )
        );
    }

    /// <summary>Verifies the 401 is a single problem document: the status-code-pages middleware does not append a second body.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Request_NoCredentials_WritesExactlyOneJsonDocument_Test()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        using var response = await client.GetAsync(ProductsUri, CancellationToken.None);

        // Assert
        var text = await response.Content.ReadAsStringAsync(CancellationToken.None);
        using var body = JsonDocument.Parse(text);
        Assert.Equal(JsonValueKind.Object, body.RootElement.ValueKind);
    }

    /// <summary>Verifies an authenticated caller who is refused by the middleware-level policy gets a 403 problem with a trace id: a non-administrator deleting is the union-driven 403 (a separate path), asserted here to still carry the same shape.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Delete_NonAdministrator_Returns403ProblemWithTraceId_Test()
    {
        // Arrange
        using var client = _factory.CreateClient().AsUser(ProductRequestMother.DefaultCallerId);

        // Act
        using var response = await client.DeleteAsync(
            $"{ProductsUri}/{ProductRequestMother.UnknownId}",
            CancellationToken.None
        );

        // Assert
        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(CancellationToken.None)
        );
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode),
            () => Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType),
            () =>
                Assert.Equal(
                    response.Headers.GetValues(TraceHeader).Single(),
                    body.RootElement.GetProperty("traceId").GetString()
                )
        );
    }

    /// <summary>
    /// Verifies the middleware's own 403 (an authenticated caller refused by the authorization policy
    /// before any action runs) is a problem body with the trace id, by tightening the fallback policy
    /// to a role nobody in the test holds.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Request_AuthenticatedButForbiddenByPolicy_Returns403ProblemWithTraceId_Test()
    {
        // Arrange
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.Configure<AuthorizationOptions>(options =>
                    options.FallbackPolicy = new AuthorizationPolicyBuilder()
                        .RequireRole("NobodyHoldsThisRole")
                        .Build()
                )
            )
        );
        using var client = factory.CreateClient().AsUser(ProductRequestMother.DefaultCallerId);

        // Act
        using var response = await client.GetAsync(ProductsUri, CancellationToken.None);

        // Assert
        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(CancellationToken.None)
        );
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode),
            () => Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType),
            () => Assert.Equal(403, body.RootElement.GetProperty("status").GetInt32()),
            () =>
                Assert.Equal(
                    response.Headers.GetValues(TraceHeader).Single(),
                    body.RootElement.GetProperty("traceId").GetString()
                )
        );
    }

    /// <summary>Verifies the health endpoints and the Development OpenAPI document answer 200 without credentials.</summary>
    /// <param name="path">The anonymous path.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    [InlineData(ApiRoutes.OpenApiV1)]
    public async Task Get_AnonymousEndpoint_Returns200WithoutCredentials_Test(string path)
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        using var response = await client.GetAsync(path, CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Verifies the OpenAPI document declares the bearer scheme and requires it, so documentation UIs can send a token.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task OpenApiDocument_DeclaresBearerSecurityScheme_Test()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        var document = await client.GetFromJsonAsync<JsonElement>(
            ApiRoutes.OpenApiV1,
            CancellationToken.None
        );

        // Assert
        var scheme = document
            .GetProperty("components")
            .GetProperty("securitySchemes")
            .GetProperty("Bearer");
        Assert.Multiple(
            () => Assert.Equal("http", scheme.GetProperty("type").GetString()),
            () => Assert.Equal("bearer", scheme.GetProperty("scheme").GetString()),
            () => Assert.Equal("JWT", scheme.GetProperty("bearerFormat").GetString()),
            () => Assert.True(document.GetProperty("security").GetArrayLength() > 0)
        );
    }

    /// <summary>Verifies the OpenAPI document mentions no identity request headers and declares 401 on every operation.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task OpenApiDocument_HasNoIdentityHeadersAndDeclares401_Test()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        var text = await client.GetStringAsync(ApiRoutes.OpenApiV1, CancellationToken.None);

        // Assert
        using var document = JsonDocument.Parse(text);
        var paths = document
            .RootElement.GetProperty("paths")
            .Deserialize<Dictionary<string, Dictionary<string, JsonElement>>>()!;
        var operationResponses = paths
            .Values.SelectMany(path => path.Values)
            .Select(operation => operation.GetProperty("responses"))
            .ToList();

        Assert.Multiple(
            () => Assert.DoesNotContain("X-Caller-Id", text, StringComparison.OrdinalIgnoreCase),
            () => Assert.DoesNotContain("X-Admin", text, StringComparison.OrdinalIgnoreCase),
            () =>
                Assert.All(
                    operationResponses,
                    responses => Assert.True(responses.TryGetProperty("401", out _))
                )
        );
    }

    /// <summary>Verifies the fallback policy is registered and the Application layer's own policies still resolve from the same authorization options.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task AuthorizationOptions_FallbackAndApplicationPolicies_AreOneCoherentSet_Test()
    {
        // Arrange
        var options = _factory.Services.GetRequiredService<IOptions<AuthorizationOptions>>().Value;
        var provider = _factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        // Act
        var administrator = await provider.GetPolicyAsync("Administrator");
        var productOwner = await provider.GetPolicyAsync("ProductOwner");

        // Assert
        Assert.Multiple(
            () => Assert.NotNull(options.FallbackPolicy),
            () => Assert.NotNull(administrator),
            () => Assert.NotNull(productOwner),
            () => Assert.Single(_factory.Services.GetServices<IAuthorizationService>())
        );
    }

    /// <summary>Verifies the framework default for inbound claim mapping is <see langword="true"/>, which the host relies on and sets explicitly.</summary>
    [Fact]
    public void JwtBearerOptions_FrameworkDefaultMapsInboundClaims_Test()
    {
        // Arrange
        var monitor = _factory.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();

        // Act
        var frameworkDefault = new JwtBearerOptions().MapInboundClaims;
        var configured = monitor.Get(JwtBearerDefaults.AuthenticationScheme).MapInboundClaims;

        // Assert
        Assert.Multiple(() => Assert.True(frameworkDefault), () => Assert.True(configured));
    }

    /// <summary>Verifies a non-Development host with no signing key configured refuses to start.</summary>
    [Fact]
    public void Start_NonDevelopmentWithoutSigningKey_FailsOptionsValidation_Test()
    {
        // Arrange
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder
                .UseEnvironment("Production")
                .UseSetting(
                    "Impersonation:SigningKey",
                    JwtTestTokens.NonDevelopmentImpersonationKey
                )
        );

        // Act
        var exception = Record.Exception(() => factory.CreateClient().Dispose());

        // Assert
        var validation = Assert.IsType<OptionsValidationException>(exception);
        Assert.Contains(
            validation.Failures,
            failure => failure.Contains("SigningKey", StringComparison.Ordinal)
        );
    }

    /// <summary>Verifies a non-Development host starts once a signing key of sufficient length is supplied.</summary>
    [Fact]
    public void Start_NonDevelopmentWithSigningKey_Starts_Test()
    {
        // Arrange
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder
                .UseEnvironment("Production")
                .UseSetting("Authentication:Jwt:SigningKey", JwtTestTokens.NonDevelopmentSigningKey)
                .UseSetting(
                    "Impersonation:SigningKey",
                    JwtTestTokens.NonDevelopmentImpersonationKey
                )
        );

        // Act
        var exception = Record.Exception(() => factory.CreateClient().Dispose());

        // Assert
        Assert.Null(exception);
    }

    /// <summary>Verifies the options validator rejects a signing key that is too short, and a missing issuer or audience.</summary>
    /// <param name="key">The configuration key to set.</param>
    /// <param name="value">The invalid value.</param>
    [Theory]
    [InlineData("Authentication:Jwt:SigningKey", "too-short")]
    [InlineData("Authentication:Jwt:Issuer", "")]
    [InlineData("Authentication:Jwt:Audience", "")]
    [InlineData("Authentication:Jwt:ClockSkewSeconds", "-1")]
    public void Start_InvalidJwtOptions_FailsOptionsValidation_Test(string key, string value)
    {
        // Arrange
        using var factory = _factory.WithWebHostBuilder(builder => builder.UseSetting(key, value));

        // Act
        var exception = Record.Exception(() => factory.CreateClient().Dispose());

        // Assert
        Assert.IsType<OptionsValidationException>(exception);
    }
}
