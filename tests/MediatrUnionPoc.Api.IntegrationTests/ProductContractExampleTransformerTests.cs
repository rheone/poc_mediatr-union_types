using System.Net.Http.Json;
using System.Text.Json.Nodes;
using MediatrUnionPoc.Api.OpenApi;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>
/// Exercises <see cref="ProductContractExampleTransformer"/> through the real generated OpenAPI
/// document — the transformer's context type is produced only by the OpenAPI document generator
/// itself, so there's no seam to construct it from directly; the served document is the only
/// observable surface.
/// </summary>
/// <remarks>
/// SWEEP-AMBIGUITY: <c>TransformAsync</c> has no argument guards (a null schema or context throws
/// NullReferenceException, not ArgumentNullException), but its parameters can only be supplied by
/// the OpenAPI generator, never null over HTTP, so no null-guard tests are written.
/// </remarks>
[Trait("Category", "Integration")]
public sealed class ProductContractExampleTransformerTests : IDisposable
{
    private const string OpenApiDocumentUri = "/openapi/v1.json";

    private readonly ProductsApiFactory _factory = new();
    private readonly HttpClient _client;

    /// <summary>Initializes a new instance of the <see cref="ProductContractExampleTransformerTests"/> class with its own <see cref="HttpClient"/>.</summary>
    public ProductContractExampleTransformerTests()
    {
        _client = _factory.CreateClient();
    }

    /// <summary>Disposes the test's <see cref="HttpClient"/> and its backing <see cref="ProductsApiFactory"/>.</summary>
    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    /// <summary>Verifies the generated document attaches the create-request example to <c>CreateProductRequest</c>'s schema.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task TransformAsync_CreateProductRequestSchema_AttachesCreateExample_Test()
    {
        // Arrange
        var schemas = await GetSchemasAsync();

        // Act
        var examples = schemas["CreateProductRequest"]!["examples"]!.AsArray();

        // Assert
        Assert.Single(examples);
        Assert.Multiple(
            () => Assert.Equal("Wireless Mouse", examples[0]!["name"]!.GetValue<string>()),
            () => Assert.Equal(24.99, examples[0]!["price"]!.GetValue<double>())
        );
    }

    /// <summary>Verifies the generated document attaches the update-request example to <c>UpdateProductRequest</c>'s schema.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task TransformAsync_UpdateProductRequestSchema_AttachesUpdateExample_Test()
    {
        // Arrange
        var schemas = await GetSchemasAsync();

        // Act
        var examples = schemas["UpdateProductRequest"]!["examples"]!.AsArray();

        // Assert
        Assert.Single(examples);
        Assert.Multiple(
            () => Assert.Equal("Wireless Mouse (v2)", examples[0]!["name"]!.GetValue<string>()),
            () => Assert.Equal(27.99, examples[0]!["price"]!.GetValue<double>())
        );
    }

    /// <summary>Verifies a schema the transformer has no example for is left without one.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task TransformAsync_ProductDtoSchema_LeavesExamplesUnset_Test()
    {
        // Arrange
        var schemas = await GetSchemasAsync();

        // Act
        var examples = schemas["ProductDto"]!["examples"];

        // Assert
        Assert.Null(examples);
    }

    /// <summary>Fetches the generated OpenAPI document and returns its <c>components.schemas</c> object.</summary>
    /// <returns>The document's <c>components.schemas</c> node.</returns>
    private async Task<JsonObject> GetSchemasAsync()
    {
        var document = await _client.GetFromJsonAsync<JsonObject>(
            OpenApiDocumentUri,
            CancellationToken.None
        );
        return document!["components"]!["schemas"]!.AsObject();
    }
}
