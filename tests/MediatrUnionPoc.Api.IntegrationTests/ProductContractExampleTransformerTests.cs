using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using MediatrUnionPoc.Api.Contracts;
using MediatrUnionPoc.Api.OpenApi;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>
/// Exercises <see cref="ProductContractExampleTransformer"/> through the real generated OpenAPI
/// document — the transformer's context type is produced only by the OpenAPI document generator
/// itself, so there's no seam to construct it from directly; the served document is the only
/// observable surface.
/// </summary>
/// <remarks>
/// The generator never passes null arguments, so the null guards on <c>TransformAsync</c> are
/// tested by calling the transformer directly with a hand-built schema and context (both are
/// publicly constructible).
/// </remarks>
[Trait("Category", "Integration")]
public sealed class ProductContractExampleTransformerTests : IDisposable
{
    private const string OpenApiDocumentUri = ApiRoutes.OpenApiV1;

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

    /// <summary>Verifies a null schema is rejected with an <see cref="ArgumentNullException"/> rather than a <see cref="NullReferenceException"/>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    // Auto Generated, verify expected behavior:
    [Fact]
    public async Task TransformAsync_NullSchema_ThrowsArgumentNullException_Test()
    {
        // Arrange
        const string parameterName = "schema";
        var transformer = new ProductContractExampleTransformer();
        var context = CreateContext(typeof(CreateProductRequest));

        // Act
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            transformer.TransformAsync(null!, context, CancellationToken.None)
        );

        // Assert
        Assert.Equal(parameterName, ex.ParamName);
    }

    /// <summary>Verifies a null context is rejected with an <see cref="ArgumentNullException"/> rather than a <see cref="NullReferenceException"/>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    // Auto Generated, verify expected behavior:
    [Fact]
    public async Task TransformAsync_NullContext_ThrowsArgumentNullException_Test()
    {
        // Arrange
        const string parameterName = "context";
        var transformer = new ProductContractExampleTransformer();

        // Act
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            transformer.TransformAsync(new OpenApiSchema(), null!, CancellationToken.None)
        );

        // Assert
        Assert.Equal(parameterName, ex.ParamName);
    }

    /// <summary>Builds a transformer context for <paramref name="type"/>; the context's members are public and settable, so it can be constructed without the OpenAPI generator.</summary>
    /// <param name="type">The CLR type the context describes.</param>
    /// <returns>A context whose <c>JsonTypeInfo</c> describes <paramref name="type"/>.</returns>
    private static OpenApiSchemaTransformerContext CreateContext(Type type) =>
        new()
        {
            DocumentName = "v1",
            JsonTypeInfo = JsonSerializerOptions.Default.GetTypeInfo(type),
            JsonPropertyInfo = null,
            ParameterDescription = null,
            ApplicationServices = null!,
        };

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
