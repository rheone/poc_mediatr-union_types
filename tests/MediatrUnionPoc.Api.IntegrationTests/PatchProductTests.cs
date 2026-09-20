using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MediatrUnionPoc.Api.Controllers;
using MediatrUnionPoc.Api.IntegrationTests.TestData;
using MediatrUnionPoc.Application.Features.Products.Common;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>
/// Exercises <c>PATCH /api/products/{id}</c> (JSON Merge Patch, RFC 7396) over real HTTP against the
/// real host and real SQLite: which fields change, what each refusal looks like on the wire, and that
/// concurrent patches cannot both win.
/// </summary>
[Trait("Category", "Integration")]
public sealed class PatchProductTests : IDisposable
{
    private const string ProductsUri = "/api/products";
    private const string MergePatchJson = "application/merge-patch+json";
    private const string ProblemJson = "application/problem+json";

    private readonly ProductsApiFactory _factory = new();
    private readonly HttpClient _client;

    /// <summary>Initializes a new instance of the <see cref="PatchProductTests"/> class with its own <see cref="HttpClient"/>.</summary>
    public PatchProductTests() =>
        _client = _factory.CreateClient().AsUser(ProductRequestMother.DefaultCallerId);

    /// <inheritdoc/>
    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    /// <summary>Verifies patching only the name returns 200 with the updated product and a new ETag, leaving the price unchanged.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task PatchAsync_NameOnly_Returns200WithNewETagAndPriceUnchanged_Test()
    {
        // Arrange
        var product = await CreateAsync("Widget", 9.99m);

        // Act
        using var response = await PatchAsync(
            product.Id.Value,
            """{"name":"Widget Pro"}""",
            ProductRequestMother.OwnerId,
            product.Version.ToETag()
        );

        // Assert
        var patched = await response.Content.ReadFromJsonAsync<ProductDto>(CancellationToken.None);
        Assert.NotNull(patched);
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.OK, response.StatusCode),
            () => Assert.Equal("W/\"2\"", response.Headers.ETag?.ToString()),
            () => Assert.Equal("Widget Pro", patched.Name),
            () => Assert.Equal(9.99m, patched.Price),
            () => Assert.Equal(2L, patched.Version.Value)
        );
    }

    /// <summary>Verifies patching only the price changes the price and leaves the name alone.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task PatchAsync_PriceOnly_Returns200WithNameUnchanged_Test()
    {
        // Arrange
        var product = await CreateAsync("Widget", 9.99m);

        // Act
        using var response = await PatchAsync(
            product.Id.Value,
            """{"price":19.99}""",
            ProductRequestMother.OwnerId,
            product.Version.ToETag()
        );

        // Assert
        var patched = await response.Content.ReadFromJsonAsync<ProductDto>(CancellationToken.None);
        Assert.NotNull(patched);
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.OK, response.StatusCode),
            () => Assert.Equal("Widget", patched.Name),
            () => Assert.Equal(19.99m, patched.Price),
            () => Assert.Equal(2L, patched.Version.Value)
        );
    }

    /// <summary>Verifies patching both fields changes both, advances the version once and persists the result.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task PatchAsync_NameAndPrice_ChangesBothBumpsVersionOnceAndKeepsCreatedAt_Test()
    {
        // Arrange
        var product = await CreateAsync("Widget", 9.99m);

        // Act
        using var response = await PatchAsync(
            product.Id.Value,
            """{"name":"Widget Pro","price":19.99}""",
            ProductRequestMother.OwnerId,
            product.Version.ToETag()
        );

        // Assert
        var stored = await _client.GetFromJsonAsync<ProductDto>(
            $"{ProductsUri}/{product.Id.Value}",
            CancellationToken.None
        );
        Assert.NotNull(stored);
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.OK, response.StatusCode),
            () => Assert.Equal("Widget Pro", stored.Name),
            () => Assert.Equal(19.99m, stored.Price),
            () => Assert.Equal(2L, stored.Version.Value),
            () => Assert.Equal(product.CreatedAt, stored.CreatedAt)
        );
    }

    /// <summary>Verifies members the contract does not know are ignored (RFC 7396) when a known member is also present — including one trying to change the owner.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task PatchAsync_UnknownMembersAlongsideKnownOne_AreIgnored_Test()
    {
        // Arrange
        var product = await CreateAsync("Widget", 9.99m);

        // Act
        using var response = await PatchAsync(
            product.Id.Value,
            """{"name":"Widget Pro","ownerId":"thief","id":"00000000-0000-0000-0000-000000000001"}""",
            ProductRequestMother.OwnerId,
            product.Version.ToETag()
        );

        // Assert
        var patched = await response.Content.ReadFromJsonAsync<ProductDto>(CancellationToken.None);
        using var asOriginalOwner = await PatchAsync(
            product.Id.Value,
            """{"price":1}""",
            ProductRequestMother.OwnerId,
            patched!.Version.ToETag()
        );
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.OK, response.StatusCode),
            () => Assert.Equal(product.Id, patched.Id),
            () => Assert.Equal(HttpStatusCode.OK, asOriginalOwner.StatusCode)
        );
    }

    /// <summary>Verifies a body naming no recognised member (empty, or only unknown members) is a 400 problem.</summary>
    /// <param name="json">The patch body.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData("{}")]
    [InlineData("""{"colour":"red"}""")]
    public async Task PatchAsync_NoRecognisedMember_Returns400Problem_Test(string json)
    {
        // Arrange
        var product = await CreateAsync("Widget", 9.99m);

        // Act
        using var response = await PatchAsync(
            product.Id.Value,
            json,
            ProductRequestMother.OwnerId,
            product.Version.ToETag()
        );

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    /// <summary>Verifies an explicit null for a required field is a per-field 400, not a request to clear it, and nothing changes.</summary>
    /// <param name="json">The patch body.</param>
    /// <param name="field">The field the error is keyed by.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData("""{"name":null}""", "Name")]
    [InlineData("""{"price":null}""", "Price")]
    [InlineData("""{"name":"","price":1}""", "Name")]
    [InlineData("""{"price":-1}""", "Price")]
    public async Task PatchAsync_NullOrInvalidField_Returns400KeyedByField_Test(
        string json,
        string field
    )
    {
        // Arrange
        var product = await CreateAsync("Widget", 9.99m);

        // Act
        using var response = await PatchAsync(
            product.Id.Value,
            json,
            ProductRequestMother.OwnerId,
            product.Version.ToETag()
        );

        // Assert
        using var body = await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        var stored = await _client.GetFromJsonAsync<ProductDto>(
            $"{ProductsUri}/{product.Id.Value}",
            CancellationToken.None
        );
        Assert.Multiple(
            () => Assert.True(body.RootElement.GetProperty("errors").TryGetProperty(field, out _)),
            () => Assert.Equal(1L, stored!.Version.Value)
        );
    }

    /// <summary>Verifies a value of the wrong JSON type is a 400 problem from the framework, not a 500.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task PatchAsync_PriceOfWrongJsonType_Returns400Problem_Test()
    {
        // Arrange
        var product = await CreateAsync("Widget", 9.99m);

        // Act
        using var response = await PatchAsync(
            product.Id.Value,
            """{"price":"cheap"}""",
            ProductRequestMother.OwnerId,
            product.Version.ToETag()
        );

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    /// <summary>Verifies plain <c>application/json</c> is refused with a 415 problem — a patch must declare its merge-patch semantics — and nothing changes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task PatchAsync_PlainJsonContentType_Returns415Problem_Test()
    {
        // Arrange
        var product = await CreateAsync("Widget", 9.99m);

        // Act
        using var response = await PatchAsync(
            product.Id.Value,
            """{"name":"Widget Pro"}""",
            ProductRequestMother.OwnerId,
            product.Version.ToETag(),
            contentType: "application/json"
        );

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.UnsupportedMediaType);
    }

    /// <summary>Verifies a missing If-Match is a 428 problem.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task PatchAsync_NoIfMatch_Returns428Problem_Test()
    {
        // Arrange
        var product = await CreateAsync("Widget", 9.99m);

        // Act
        using var response = await PatchAsync(
            product.Id.Value,
            """{"name":"Widget Pro"}""",
            ProductRequestMother.OwnerId
        );

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.PreconditionRequired);
    }

    /// <summary>Verifies a malformed If-Match is a 400 problem.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task PatchAsync_MalformedIfMatch_Returns400Problem_Test()
    {
        // Arrange
        var product = await CreateAsync("Widget", 9.99m);

        // Act
        using var response = await PatchAsync(
            product.Id.Value,
            """{"name":"Widget Pro"}""",
            ProductRequestMother.OwnerId,
            "not-an-etag"
        );

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    /// <summary>Verifies a stale If-Match is a 412 problem and leaves the product unchanged.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task PatchAsync_StaleIfMatch_Returns412Problem_Test()
    {
        // Arrange
        var product = await CreateAsync("Widget", 9.99m);
        using var first = await PatchAsync(
            product.Id.Value,
            """{"price":2}""",
            ProductRequestMother.OwnerId,
            product.Version.ToETag()
        );

        // Act
        using var response = await PatchAsync(
            product.Id.Value,
            """{"price":3}""",
            ProductRequestMother.OwnerId,
            product.Version.ToETag()
        );

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.PreconditionFailed);
        var stored = await _client.GetFromJsonAsync<ProductDto>(
            $"{ProductsUri}/{product.Id.Value}",
            CancellationToken.None
        );
        Assert.Equal(2m, stored!.Price);
    }

    /// <summary>Verifies a caller who does not own the product gets a 403 problem and the product is unchanged.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task PatchAsync_NonOwner_Returns403Problem_Test()
    {
        // Arrange
        var product = await CreateAsync("Widget", 9.99m);

        // Act
        using var response = await PatchAsync(
            product.Id.Value,
            """{"name":"Hijacked"}""",
            ProductRequestMother.OtherCallerId,
            product.Version.ToETag()
        );

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.Forbidden);
        var stored = await _client.GetFromJsonAsync<ProductDto>(
            $"{ProductsUri}/{product.Id.Value}",
            CancellationToken.None
        );
        Assert.Equal("Widget", stored!.Name);
    }

    /// <summary>Verifies patching an unknown product is a 404 problem.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task PatchAsync_UnknownId_Returns404Problem_Test()
    {
        // Act
        using var response = await PatchAsync(
            ProductRequestMother.UnknownId,
            """{"name":"Widget Pro"}""",
            ProductRequestMother.OwnerId,
            "W/\"1\""
        );

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    /// <summary>Verifies renaming onto another product's name (ignoring case and padding) is a 409 problem and leaves the product unchanged.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task PatchAsync_NameHeldByAnotherProduct_Returns409Problem_Test()
    {
        // Arrange
        await CreateAsync("Blue Widget", 1m);
        var red = await CreateAsync("Red Widget", 1m);

        // Act
        using var response = await PatchAsync(
            red.Id.Value,
            """{"name":" blue WIDGET"}""",
            ProductRequestMother.OwnerId,
            red.Version.ToETag()
        );

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.Conflict);
        var stored = await _client.GetFromJsonAsync<ProductDto>(
            $"{ProductsUri}/{red.Id.Value}",
            CancellationToken.None
        );
        Assert.Equal("Red Widget", stored!.Name);
    }

    /// <summary>Verifies patching a product to its own current name (re-cased) is not a conflict.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task PatchAsync_OwnCurrentNameRecased_Returns200_Test()
    {
        // Arrange
        var blue = await CreateAsync("Blue Widget", 1m);

        // Act
        using var response = await PatchAsync(
            blue.Id.Value,
            """{"name":"BLUE WIDGET"}""",
            ProductRequestMother.OwnerId,
            blue.Version.ToETag()
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Verifies simultaneous patches carrying the same ETag resolve to exactly one 200 and the rest 412 — never two winners, never a 500.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task PatchAsync_RacingPatchesWithSameETag_ExactlyOneSucceedsAndTheRestGet412_Test()
    {
        // Arrange
        const int attempts = 4;
        var product = await CreateAsync("Widget", 9.99m);
        var etag = product.Version.ToETag();

        // Act
        var responses = await Task.WhenAll(
            Enumerable
                .Range(0, attempts)
                .Select(index =>
                    PatchAsync(
                        product.Id.Value,
                        $$"""{"name":"Writer {{index}}"}""",
                        ProductRequestMother.OwnerId,
                        etag
                    )
                )
        );

        // Assert
        var statuses = responses.Select(r => r.StatusCode).OrderBy(s => (int)s).ToList();
        foreach (var response in responses)
        {
            response.Dispose();
        }

        Assert.Equal(
            [
                HttpStatusCode.OK,
                .. Enumerable.Repeat(HttpStatusCode.PreconditionFailed, attempts - 1),
            ],
            statuses
        );
    }

    private static async Task<JsonDocument> AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expected
    )
    {
        var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(CancellationToken.None)
        );
        Assert.Multiple(
            () => Assert.Equal(expected, response.StatusCode),
            () => Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType),
            () => Assert.Equal((int)expected, body.RootElement.GetProperty("status").GetInt32()),
            () =>
                Assert.Equal(
                    response.Headers.GetValues("X-Trace-Id").Single(),
                    body.RootElement.GetProperty("traceId").GetString()
                )
        );
        return body;
    }

    private async Task<ProductDto> CreateAsync(string name, decimal price)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, ProductsUri)
        {
            Content = JsonContent.Create(new { name, price }),
        }.AsUser(ProductRequestMother.OwnerId);
        using var response = await _client.SendAsync(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductDto>(CancellationToken.None))!;
    }

    private async Task<HttpResponseMessage> PatchAsync(
        Guid id,
        string json,
        string? callerId = null,
        string? ifMatch = null,
        string contentType = MergePatchJson
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"{ProductsUri}/{id}")
        {
            Content = new StringContent(json, Encoding.UTF8, new MediaTypeHeaderValue(contentType)),
        };

        if (callerId is not null)
        {
            request.AsUser(callerId);
        }

        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return await _client.SendAsync(request, CancellationToken.None);
    }
}
