using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>Verifies the generated OpenAPI document describes the ETag / If-Match concurrency contract.</summary>
[Trait("Category", "Integration")]
public sealed class ConcurrencyOpenApiTests : IDisposable
{
    private const string OpenApiDocumentUri = ApiRoutes.OpenApiV1;
    private const string ItemPath = ApiRoutes.ProductByIdTemplate;
    private const string ListPath = ApiRoutes.Products;

    private readonly ProductsApiFactory _factory = new();
    private readonly HttpClient _client;

    /// <summary>Initializes a new instance of the <see cref="ConcurrencyOpenApiTests"/> class with its own <see cref="HttpClient"/>.</summary>
    public ConcurrencyOpenApiTests() => _client = _factory.CreateClient();

    /// <summary>Disposes the test's <see cref="HttpClient"/> and its backing <see cref="ProductsApiFactory"/>.</summary>
    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    /// <summary>Verifies PUT documents 412 and 428 responses.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Document_PutOperation_DeclaresPreconditionResponses_Test()
    {
        // Arrange
        var responses = await ResponsesAsync(ItemPath, "put");

        // Act
        var codes = responses.Select(r => r.Key).ToList();

        // Assert
        Assert.Multiple(() => Assert.Contains("412", codes), () => Assert.Contains("428", codes));
    }

    /// <summary>Verifies every success response that returns a product's version documents the ETag header.</summary>
    /// <param name="path">The path.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="status">The success status.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData(ListPath, "post", "201")]
    [InlineData(ItemPath, "get", "200")]
    [InlineData(ItemPath, "put", "204")]
    public async Task Document_SuccessResponse_DeclaresETagHeader_Test(
        string path,
        string method,
        string status
    )
    {
        // Arrange
        var responses = await ResponsesAsync(path, method);

        // Act
        var header = responses[status]!["headers"]?["ETag"];

        // Assert
        Assert.NotNull(header);
    }

    /// <summary>Verifies the 412 and 428 responses carry a problem-details example.</summary>
    /// <param name="status">The precondition status.</param>
    /// <param name="expectedStatusInExample">The <c>status</c> the example body shows.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData("412", 412)]
    [InlineData("428", 428)]
    public async Task Document_PreconditionResponse_HasProblemExample_Test(
        string status,
        int expectedStatusInExample
    )
    {
        // Arrange
        var responses = await ResponsesAsync(ItemPath, "put");

        // Act
        var content = responses[status]!["content"]!.AsObject().FirstOrDefault().Value!;
        var example =
            content["example"] ?? content["examples"]?.AsObject().FirstOrDefault().Value?["value"];

        // Assert
        Assert.Equal(expectedStatusInExample, example!["status"]!.GetValue<int>());
    }

    private async Task<JsonObject> ResponsesAsync(string path, string method)
    {
        var document = await _client.GetFromJsonAsync<JsonObject>(
            OpenApiDocumentUri,
            CancellationToken.None
        );
        return document!["paths"]![path]![method]!["responses"]!.AsObject();
    }
}
