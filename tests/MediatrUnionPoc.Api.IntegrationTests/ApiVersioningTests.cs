using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MediatrUnionPoc.Api.Contracts;
using MediatrUnionPoc.Api.IntegrationTests.TestData;
using MediatrUnionPoc.Application.Features.Products.Common;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>
/// Exercises URL-segment API versioning over real HTTP: the versioned routes, the transitional
/// unversioned alias (which must behave identically), the versions the API reports, the versioned
/// URLs it generates (<c>Location</c>, <c>Link</c>), how a version that does not exist is answered,
/// and the per-version OpenAPI document. The alias tests are the only ones that call
/// <see cref="ApiRoutes.UnversionedProducts"/> and <see cref="ApiRoutes.UnversionedImpersonationTokens"/>.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ApiVersioningTests : IDisposable
{
    private const string ProblemJson = "application/problem+json";
    private const string TraceHeader = "X-Trace-Id";

    private readonly ProductsApiFactory _factory = new();
    private readonly HttpClient _client;

    /// <summary>Initializes a new instance of the <see cref="ApiVersioningTests"/> class with a client acting as an ordinary caller.</summary>
    public ApiVersioningTests() =>
        _client = _factory.CreateClient().AsUser(ProductRequestMother.DefaultCallerId);

    /// <summary>Disposes the test's <see cref="HttpClient"/> and its backing <see cref="ProductsApiFactory"/>.</summary>
    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    /// <summary>Verifies a create through the unversioned alias behaves as the versioned route does: 201, an ETag, and a Location on the versioned URL of the new product.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Post_UnversionedAlias_MatchesVersionedRouteAndLocationIsVersioned_Test()
    {
        // Act
        using var viaAlias = await _client.PostAsJsonAsync(
            ApiRoutes.UnversionedProducts,
            ProductRequestMother.Widget(),
            CancellationToken.None
        );
        using var viaVersioned = await _client.PostAsJsonAsync(
            ApiRoutes.Products,
            new CreateProductRequest("Widget Pro", 19.99m),
            CancellationToken.None
        );

        // Assert
        var aliasDto = await viaAlias.Content.ReadFromJsonAsync<ProductDto>(CancellationToken.None);
        var versionedDto = await viaVersioned.Content.ReadFromJsonAsync<ProductDto>(
            CancellationToken.None
        );
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.Created, viaAlias.StatusCode),
            () => Assert.Equal(HttpStatusCode.Created, viaVersioned.StatusCode),
            () => Assert.Equal("W/\"1\"", viaAlias.Headers.ETag?.ToString()),
            () =>
                Assert.Equal(
                    $"{ApiRoutes.Products}/{aliasDto!.Id.Value}",
                    viaAlias.Headers.Location?.ToString()
                ),
            () =>
                Assert.Equal(
                    $"{ApiRoutes.Products}/{versionedDto!.Id.Value}",
                    viaVersioned.Headers.Location?.ToString()
                )
        );
    }

    /// <summary>Verifies the Location a versioned create returns can be followed and finds the product.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Post_VersionedRoute_LocationIsFollowable_Test()
    {
        // Arrange
        using var created = await _client.PostAsJsonAsync(
            ApiRoutes.Products,
            ProductRequestMother.Widget(),
            CancellationToken.None
        );

        // Act
        using var located = await _client.GetAsync(
            created.Headers.Location,
            CancellationToken.None
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, located.StatusCode);
    }

    /// <summary>Verifies a get by id through the alias answers exactly as the versioned route does.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_ByIdViaUnversionedAlias_MatchesVersionedRoute_Test()
    {
        // Arrange
        var (id, _) = await CreateWidgetAsync();

        // Act
        using var viaAlias = await _client.GetAsync(
            $"{ApiRoutes.UnversionedProducts}/{id}",
            CancellationToken.None
        );
        using var viaVersioned = await _client.GetAsync(
            $"{ApiRoutes.Products}/{id}",
            CancellationToken.None
        );

        // Assert
        var aliasBody = await viaAlias.Content.ReadAsStringAsync(CancellationToken.None);
        var versionedBody = await viaVersioned.Content.ReadAsStringAsync(CancellationToken.None);
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.OK, viaAlias.StatusCode),
            () => Assert.Equal(viaVersioned.StatusCode, viaAlias.StatusCode),
            () => Assert.Equal(viaVersioned.Headers.ETag, viaAlias.Headers.ETag),
            () => Assert.Equal(versionedBody, aliasBody)
        );
    }

    /// <summary>Verifies a get of a missing id through the alias is the same 404 problem the versioned route gives.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_MissingIdViaUnversionedAlias_Returns404Problem_Test()
    {
        // Act
        using var response = await _client.GetAsync(
            $"{ApiRoutes.UnversionedProducts}/{ProductRequestMother.UnknownId}",
            CancellationToken.None
        );

        // Assert
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.NotFound, response.StatusCode),
            () => Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType)
        );
    }

    /// <summary>Verifies a listing through the alias returns the paging headers with every Link URL on the versioned route, keeping the request's query.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_ListViaUnversionedAlias_LinkHeaderUsesVersionedUrl_Test()
    {
        // Arrange
        await CreateWidgetAsync();

        // Act
        using var response = await _client.GetAsync(
            $"{ApiRoutes.UnversionedProducts}?pageSize=1&nameContains=widget",
            CancellationToken.None
        );

        // Assert
        var link = response.Headers.GetValues("Link").Single();
        const string Url =
            $"http://localhost{ApiRoutes.Products}?pageSize=1&nameContains=widget&pageNumber=1";
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.OK, response.StatusCode),
            () => Assert.Equal("1", response.Headers.GetValues("X-Total-Count").Single()),
            () => Assert.Equal($"<{Url}>; rel=\"first\", <{Url}>; rel=\"last\"", link)
        );
    }

    /// <summary>Verifies the Link header of a listing through the versioned route is on the versioned URL.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_ListViaVersionedRoute_LinkHeaderUsesVersionedUrl_Test()
    {
        // Act
        using var response = await _client.GetAsync(ApiRoutes.Products, CancellationToken.None);

        // Assert
        Assert.StartsWith(
            $"<http://localhost{ApiRoutes.Products}?",
            response.Headers.GetValues("Link").Single(),
            StringComparison.Ordinal
        );
    }

    /// <summary>Verifies a version written with a minor part (<c>v1.0</c>) reaches the same operation, and the URLs it generates still use the canonical <c>v1</c> spelling.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_ListWithMinorVersionSegment_IsServedAndLinksAreCanonical_Test()
    {
        // Act
        using var response = await _client.GetAsync("/api/v1.0/products", CancellationToken.None);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.OK, response.StatusCode),
            () =>
                Assert.StartsWith(
                    $"<http://localhost{ApiRoutes.Products}?",
                    response.Headers.GetValues("Link").Single(),
                    StringComparison.Ordinal
                )
        );
    }

    /// <summary>Verifies a replace through the alias behaves as the versioned route does: 204 with the next ETag.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Put_ViaUnversionedAlias_Returns204WithNextETag_Test()
    {
        // Arrange
        var (id, etag) = await CreateWidgetAsync();
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"{ApiRoutes.UnversionedProducts}/{id}"
        )
        {
            Content = JsonContent.Create(ProductRequestMother.WidgetPro()),
        };
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        using var response = await _client.SendAsync(request, CancellationToken.None);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.NoContent, response.StatusCode),
            () => Assert.Equal("W/\"2\"", response.Headers.ETag?.ToString())
        );
    }

    /// <summary>Verifies a merge patch through the alias behaves as the versioned route does: 200 with the patched product, and 415 for another media type.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Patch_ViaUnversionedAlias_Returns200AndKeepsMediaTypeRule_Test()
    {
        // Arrange
        var (id, etag) = await CreateWidgetAsync();
        var uri = $"{ApiRoutes.UnversionedProducts}/{id}";
        using var patch = PatchRequest(uri, etag, "application/merge-patch+json");
        using var wrongType = PatchRequest(uri, etag, "application/json");

        // Act
        using var patched = await _client.SendAsync(patch, CancellationToken.None);
        using var refused = await _client.SendAsync(wrongType, CancellationToken.None);

        // Assert
        var dto = await patched.Content.ReadFromJsonAsync<ProductDto>(CancellationToken.None);
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.OK, patched.StatusCode),
            () => Assert.Equal(14.99m, dto!.Price),
            () => Assert.Equal(HttpStatusCode.UnsupportedMediaType, refused.StatusCode)
        );
    }

    /// <summary>Verifies a delete through the alias behaves as the versioned route does: 204 for an administrator, then 404 through either route.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Delete_ViaUnversionedAlias_Returns204ThenNotFound_Test()
    {
        // Arrange
        var (id, _) = await CreateWidgetAsync();
        using var admin = _factory
            .CreateClient()
            .AsUser(ProductRequestMother.AdminCallerId, ProductRequestMother.AdministratorRole);

        // Act
        using var deleted = await admin.DeleteAsync(
            $"{ApiRoutes.UnversionedProducts}/{id}",
            CancellationToken.None
        );
        using var again = await admin.DeleteAsync(
            $"{ApiRoutes.Products}/{id}",
            CancellationToken.None
        );

        // Assert
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode),
            () => Assert.Equal(HttpStatusCode.NotFound, again.StatusCode)
        );
    }

    /// <summary>Verifies the impersonation endpoint answers identically on its versioned route and its unversioned alias, refusal included.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task ImpersonationTokens_UnversionedAlias_MatchesVersionedRoute_Test()
    {
        // Arrange
        using var jwtFactory = new ProductsApiFactory(ApiAuthentication.RealJwt);
        using var support = ImpersonationTestSupport.ClientAs(
            jwtFactory,
            ImpersonationTestSupport.SupportId,
            "Support"
        );
        using var plain = ImpersonationTestSupport.ClientAs(
            jwtFactory,
            ImpersonationTestSupport.UserId
        );
        var body = ImpersonationTestSupport.Body();

        // Act
        using var aliasOk = await support.PostAsJsonAsync(
            ApiRoutes.UnversionedImpersonationTokens,
            body,
            CancellationToken.None
        );
        using var versionedOk = await support.PostAsJsonAsync(
            ApiRoutes.ImpersonationTokens,
            body,
            CancellationToken.None
        );
        using var aliasDenied = await plain.PostAsJsonAsync(
            ApiRoutes.UnversionedImpersonationTokens,
            body,
            CancellationToken.None
        );

        // Assert
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.OK, aliasOk.StatusCode),
            () => Assert.Equal(HttpStatusCode.OK, versionedOk.StatusCode),
            () => Assert.Equal("no-store", aliasOk.Headers.CacheControl?.ToString()),
            () => Assert.Equal(HttpStatusCode.Forbidden, aliasDenied.StatusCode)
        );
    }

    /// <summary>Verifies every response to a versioned or aliased route reports the supported versions, whatever its status (the last row is a 404).</summary>
    /// <param name="path">The route.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData(ApiRoutes.Products)]
    [InlineData(ApiRoutes.UnversionedProducts)]
    [InlineData(ApiRoutes.Products + "/00000000-0000-0000-0000-00000000000a")]
    public async Task Get_ProductRoutes_ReportSupportedVersions_Test(string path)
    {
        // Act
        using var response = await _client.GetAsync(path, CancellationToken.None);

        // Assert
        Assert.Equal(
            "1.0",
            Assert.Single(response.Headers.GetValues(ApiRoutes.SupportedVersionsHeader))
        );
    }

    /// <summary>Verifies the version-neutral endpoints (health, OpenAPI) are unaffected: reachable at their plain paths and not versioned.</summary>
    /// <param name="path">The path.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    [InlineData(ApiRoutes.OpenApiV1)]
    public async Task Get_NeutralEndpoint_Returns200WithoutVersionHeader_Test(string path)
    {
        // Arrange
        using var anonymous = _factory.CreateClient();

        // Act
        using var response = await anonymous.GetAsync(path, CancellationToken.None);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.OK, response.StatusCode),
            () => Assert.False(response.Headers.Contains(ApiRoutes.SupportedVersionsHeader))
        );
    }

    /// <summary>
    /// Verifies a version this API does not serve, or a segment that is no version at all, is a 404
    /// problem with the trace id in body and header. With the version in the URL the address simply
    /// does not exist, so the framework's not-found problem is the honest answer.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData("/api/v9/products")]
    [InlineData("/api/v2/products/00000000-0000-0000-0000-00000000000a")]
    [InlineData("/api/vabc/products")]
    [InlineData("/api/v9/impersonation/tokens")]
    public async Task Get_UnservedVersion_Returns404ProblemWithTraceId_Test(string path)
    {
        // Act
        using var response = await _client.GetAsync(path, CancellationToken.None);

        // Assert
        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(CancellationToken.None)
        );
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.NotFound, response.StatusCode),
            () => Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType),
            () =>
                Assert.Equal(
                    response.Headers.GetValues(TraceHeader).Single(),
                    body.RootElement.GetProperty("traceId").GetString()
                )
        );
    }

    /// <summary>Verifies an anonymous caller asking for a version that does not exist gets the fallback policy's 401 problem (with trace id), as for any other unmatched route.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Get_UnservedVersionAnonymous_Returns401ProblemWithTraceId_Test()
    {
        // Arrange
        using var anonymous = _factory.CreateClient();

        // Act
        using var response = await anonymous.GetAsync("/api/v9/products", CancellationToken.None);

        // Assert
        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(CancellationToken.None)
        );
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode),
            () => Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType),
            () =>
                Assert.Equal(
                    response.Headers.GetValues(TraceHeader).Single(),
                    body.RootElement.GetProperty("traceId").GetString()
                )
        );
    }

    /// <summary>Verifies the v1 OpenAPI document lists only versioned paths: no unversioned alias, no health or OpenAPI endpoints.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task OpenApiV1_ListsOnlyVersionedPaths_Test()
    {
        // Act
        var document = (
            await _client.GetFromJsonAsync<JsonObject>(ApiRoutes.OpenApiV1, CancellationToken.None)
        )!;

        // Assert
        var paths = document["paths"]!.AsObject().Select(path => path.Key).ToList();
        Assert.Multiple(
            () => Assert.NotEmpty(paths),
            () =>
                Assert.All(
                    paths,
                    path => Assert.StartsWith("/api/v1/", path, StringComparison.Ordinal)
                ),
            () => Assert.Contains(ApiRoutes.Products, paths),
            () => Assert.Contains(ApiRoutes.ProductByIdTemplate, paths),
            () => Assert.Contains(ApiRoutes.ImpersonationTokens, paths)
        );
    }

    /// <summary>Verifies the alias does not duplicate operations in the document: each verb appears once per versioned path.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task OpenApiV1_DocumentsEachOperationOnce_Test()
    {
        // Act
        var document = (
            await _client.GetFromJsonAsync<JsonObject>(ApiRoutes.OpenApiV1, CancellationToken.None)
        )!;

        // Assert
        var operations = document["paths"]!
            .AsObject()
            .SelectMany(path => path.Value!.AsObject().Select(op => $"{op.Key} {path.Key}"))
            .Order(StringComparer.Ordinal)
            .ToList();
        Assert.Equal(
            [
                $"delete {ApiRoutes.ProductByIdTemplate}",
                $"get {ApiRoutes.Products}",
                $"get {ApiRoutes.ProductByIdTemplate}",
                $"patch {ApiRoutes.ProductByIdTemplate}",
                $"post {ApiRoutes.ImpersonationTokens}",
                $"post {ApiRoutes.Products}",
                $"put {ApiRoutes.ProductByIdTemplate}",
            ],
            operations
        );
    }

    /// <summary>Verifies only the versions that exist have a document.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task OpenApi_UnknownVersionDocument_Returns404_Test()
    {
        // Act
        using var response = await _client.GetAsync("/openapi/v2.json", CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Verifies the Scalar reference lists the versioned OpenAPI document as its source.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Scalar_ListsTheVersionedDocument_Test()
    {
        // Act
        var page = await _client.GetStringAsync("/scalar/v1", CancellationToken.None);

        // Assert
        Assert.Contains("openapi/v1.json", page, StringComparison.Ordinal);
    }

    private static HttpRequestMessage PatchRequest(string uri, string etag, string mediaType)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, uri)
        {
            Content = new StringContent("{\"price\":14.99}", Encoding.UTF8, mediaType),
        };
        request.Headers.TryAddWithoutValidation("If-Match", etag);
        return request;
    }

    private async Task<(Guid Id, string ETag)> CreateWidgetAsync()
    {
        using var response = await _client.PostAsJsonAsync(
            ApiRoutes.Products,
            ProductRequestMother.Widget(),
            CancellationToken.None
        );
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<ProductDto>(CancellationToken.None);

        return (dto!.Id.Value, response.Headers.ETag!.ToString());
    }
}
