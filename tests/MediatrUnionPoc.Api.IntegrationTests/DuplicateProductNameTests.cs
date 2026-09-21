using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using MediatrUnionPoc.Api.Contracts;
using MediatrUnionPoc.Api.Controllers;
using MediatrUnionPoc.Api.IntegrationTests.TestData;
using MediatrUnionPoc.Application.Features.Products.Common;
using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>
/// Exercises the "product names are unique" rule over real HTTP against the real host and real
/// SQLite: a name is a duplicate if it matches another product's name ignoring case and
/// surrounding whitespace, enforced by an up-front check and, for races, by a unique index.
/// </summary>
[Trait("Category", "Integration")]
public sealed class DuplicateProductNameTests : IDisposable
{
    private const string ProductsUri = ApiRoutes.Products;
    private const string ProblemJson = "application/problem+json";
    private const string BlueWidget = "Blue Widget";
    private const string RedWidget = "Red Widget";

    private readonly ProductsApiFactory _factory = new();
    private readonly HttpClient _client;

    /// <summary>Initializes a new instance of the <see cref="DuplicateProductNameTests"/> class with its own <see cref="HttpClient"/>.</summary>
    public DuplicateProductNameTests() =>
        _client = _factory.CreateClient().AsUser(ProductRequestMother.DefaultCallerId);

    /// <inheritdoc/>
    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    /// <summary>Verifies creating a product whose name differs from an existing one only by case and padding is a 409 problem that names the supplied name.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task CreateAsync_NameDuplicatesExistingIgnoringCaseAndPadding_Returns409Problem_Test()
    {
        // Arrange
        using var first = await SendAsync(
            HttpMethod.Post,
            ProductsUri,
            ProductRequestMother.Named(BlueWidget)
        );

        // Act
        using var response = await SendAsync(
            HttpMethod.Post,
            ProductsUri,
            ProductRequestMother.Named("  BLUE widget ")
        );

        // Assert
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);
        using var json = JsonDocument.Parse(body);
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.Created, first.StatusCode),
            () => Assert.Equal(HttpStatusCode.Conflict, response.StatusCode),
            () => Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType),
            () => Assert.Equal(409, json.RootElement.GetProperty("status").GetInt32()),
            () =>
                Assert.Contains(
                    "'  BLUE widget '",
                    json.RootElement.GetProperty("detail").GetString(),
                    StringComparison.Ordinal
                )
        );
    }

    /// <summary>Verifies renaming a product onto another product's name is a 409 and leaves it unchanged.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task UpdateAsync_RenameOntoAnotherProductsName_Returns409AndLeavesProductUnchanged_Test()
    {
        // Arrange
        await CreateAsync(BlueWidget, ProductRequestMother.OwnerId);
        var red = await CreateAsync(RedWidget, ProductRequestMother.OwnerId);
        var uri = $"{ProductsUri}/{red.Id.Value}";

        // Act
        using var response = await SendAsync(
            HttpMethod.Put,
            uri,
            new UpdateProductRequest(" blue WIDGET", 2m),
            ProductRequestMother.OwnerId,
            ifMatch: red.Version.ToETag()
        );

        // Assert
        var fetched = await _client.GetFromJsonAsync<ProductDto>(uri, CancellationToken.None);
        Assert.NotNull(fetched);
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.Conflict, response.StatusCode),
            () => Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType),
            () => Assert.Equal(RedWidget, fetched.Name)
        );
    }

    /// <summary>Verifies resubmitting a product's own name (even re-cased) with a new price is not a conflict with itself.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task UpdateAsync_KeepingOwnNameWithDifferentCase_Returns204_Test()
    {
        // Arrange
        var blue = await CreateAsync(BlueWidget, ProductRequestMother.OwnerId);

        // Act
        using var response = await SendAsync(
            HttpMethod.Put,
            $"{ProductsUri}/{blue.Id.Value}",
            new UpdateProductRequest("BLUE WIDGET", 5m),
            ProductRequestMother.OwnerId,
            ifMatch: blue.Version.ToETag()
        );

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>Verifies two simultaneous creates of the same name resolve to exactly one 201 and one 409 — never two winners, never a 500.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task CreateAsync_TwoRacingCreatesOfSameName_ExactlyOneCreatedAndOneConflict_Test()
    {
        // Arrange
        const int attempts = 8;

        // Act
        var responses = await Task.WhenAll(
            Enumerable
                .Range(0, attempts)
                .Select(_ =>
                    SendAsync(HttpMethod.Post, ProductsUri, ProductRequestMother.Named("Racer"))
                )
        );

        // Assert
        var statuses = responses.Select(r => r.StatusCode).OrderBy(s => (int)s).ToList();
        foreach (var response in responses)
        {
            response.Dispose();
        }

        var page = await _client.GetFromJsonAsync<PagedResult<ProductDto>>(
            ProductsUri,
            CancellationToken.None
        );
        Assert.Multiple(
            () => Assert.Equal(1, statuses.Count(s => s == HttpStatusCode.Created)),
            () => Assert.Equal(attempts - 1, statuses.Count(s => s == HttpStatusCode.Conflict)),
            () => Assert.Equal(1, page!.TotalCount)
        );
    }

    /// <summary>Verifies POST and PUT document a 409 response carrying a problem-details example that names the conflicting field.</summary>
    /// <param name="path">The OpenAPI path key of the operation under test.</param>
    /// <param name="method">The lower-case OpenAPI operation key (HTTP method).</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData(ApiRoutes.Products, "post")]
    [InlineData(ApiRoutes.ProductByIdTemplate, "put")]
    public async Task Document_ConflictResponse_HasProblemExampleNamingTheProduct_Test(
        string path,
        string method
    )
    {
        // Arrange
        var document = await _client.GetFromJsonAsync<JsonObject>(
            ApiRoutes.OpenApiV1,
            CancellationToken.None
        );
        var responses = document!["paths"]![path]![method]!["responses"]!.AsObject();

        // Act
        var content = responses["409"]!["content"]!.AsObject().FirstOrDefault().Value!;
        var example =
            content["example"] ?? content["examples"]?.AsObject().FirstOrDefault().Value?["value"];

        // Assert
        Assert.Multiple(
            () => Assert.Equal(409, example!["status"]!.GetValue<int>()),
            () =>
                Assert.Contains(
                    "product named",
                    example!["detail"]!.GetValue<string>(),
                    StringComparison.Ordinal
                )
        );
    }

    private async Task<ProductDto> CreateAsync(string name, string callerId)
    {
        using var response = await SendAsync(
            HttpMethod.Post,
            ProductsUri,
            ProductRequestMother.Named(name),
            callerId
        );
        return (await response.Content.ReadFromJsonAsync<ProductDto>(CancellationToken.None))!;
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string requestUri,
        object? body = null,
        string? callerId = null,
        string? ifMatch = null
    )
    {
        using var request = new HttpRequestMessage(method, requestUri);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType());
        }

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
