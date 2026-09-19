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
public sealed class ProductContractExampleTransformerTests : IDisposable
{
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
    public async Task CreateProductRequest_schema_carries_its_example()
    {
        var schemas = await GetSchemasAsync();

        var examples = schemas["CreateProductRequest"]!["examples"]!.AsArray();

        Assert.Single(examples);
        Assert.Equal("Wireless Mouse", examples[0]!["name"]!.GetValue<string>());
        Assert.Equal(24.99, examples[0]!["price"]!.GetValue<double>());
    }

    /// <summary>Verifies the generated document attaches the update-request example to <c>UpdateProductRequest</c>'s schema.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task UpdateProductRequest_schema_carries_its_example()
    {
        var schemas = await GetSchemasAsync();

        var examples = schemas["UpdateProductRequest"]!["examples"]!.AsArray();

        Assert.Single(examples);
        Assert.Equal("Wireless Mouse (v2)", examples[0]!["name"]!.GetValue<string>());
        Assert.Equal(27.99, examples[0]!["price"]!.GetValue<double>());
    }

    /// <summary>Verifies a schema the transformer has no example for is left without one.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task ProductDto_schema_has_no_example_attached()
    {
        var schemas = await GetSchemasAsync();

        Assert.Null(schemas["ProductDto"]!["examples"]);
    }

    /// <summary>Fetches the generated OpenAPI document and returns its <c>components.schemas</c> object.</summary>
    /// <returns>The document's <c>components.schemas</c> node.</returns>
    private async Task<JsonObject> GetSchemasAsync()
    {
        var document = await _client.GetFromJsonAsync<JsonObject>("/openapi/v1.json");
        return document!["components"]!["schemas"]!.AsObject();
    }
}
