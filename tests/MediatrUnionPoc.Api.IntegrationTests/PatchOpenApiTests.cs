using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>Verifies the generated OpenAPI document describes <c>PATCH /api/v1/products/{id}</c> as a JSON Merge Patch operation.</summary>
[Trait("Category", "Integration")]
public sealed class PatchOpenApiTests : IDisposable
{
    private const string MergePatchJson = "application/merge-patch+json";

    private readonly ProductsApiFactory _factory = new();
    private readonly HttpClient _client;

    /// <summary>Initializes a new instance of the <see cref="PatchOpenApiTests"/> class with its own <see cref="HttpClient"/>.</summary>
    public PatchOpenApiTests() => _client = _factory.CreateClient();

    /// <inheritdoc/>
    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    /// <summary>Verifies the request body is declared as <c>application/merge-patch+json</c> and only that.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Document_PatchOperation_ConsumesOnlyMergePatchJson_Test()
    {
        // Arrange
        var operation = await PatchOperationAsync();

        // Act
        var mediaTypes = operation["requestBody"]!["content"]!
            .AsObject()
            .Select(p => p.Key)
            .ToList();

        // Assert
        Assert.Equal([MergePatchJson], mediaTypes);
    }

    /// <summary>Verifies the patch body's members are described as optional strings and numbers, not as a wrapper object, and neither is required.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Document_PatchBodySchema_DescribesOptionalNameAndPrice_Test()
    {
        // Arrange
        var schema = await PatchBodySchemaAsync();

        // Act
        var properties = schema["properties"]!.AsObject();
        var required =
            schema["required"]?.AsArray().Select(n => n!.GetValue<string>()).ToList() ?? [];

        // Assert
        Assert.Multiple(
            () => Assert.Equal(["name", "price"], properties.Select(p => p.Key).Order().ToList()),
            () => Assert.Contains("string", Types(properties["name"]!)),
            () => Assert.Contains("number", Types(properties["price"]!)),
            () => Assert.DoesNotContain("name", required),
            () => Assert.DoesNotContain("price", required)
        );
    }

    /// <summary>Verifies the patch body schema carries an example naming a single field, showing that a patch need not be complete.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Document_PatchBodySchema_HasPartialExample_Test()
    {
        // Arrange
        var schema = await PatchBodySchemaAsync();

        // Act
        var example = schema["examples"]?[0] ?? schema["example"];

        // Assert
        Assert.Multiple(
            () => Assert.NotNull(example),
            () => Assert.Equal("Wireless Mouse (v3)", example!["name"]!.GetValue<string>()),
            () => Assert.Null(example!["price"])
        );
    }

    /// <summary>Verifies every response the action can produce is declared, the success one with its ETag header.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Document_PatchOperation_DeclaresEveryResponse_Test()
    {
        // Arrange
        var operation = await PatchOperationAsync();

        // Act
        var responses = operation["responses"]!.AsObject();

        // Assert
        Assert.Multiple(
            () =>
                Assert.Equal(
                    [
                        "200",
                        "400",
                        "401",
                        "403",
                        "404",
                        "409",
                        "412",
                        "415",
                        "428",
                        "429",
                        "500",
                        "504",
                    ],
                    responses.Select(r => r.Key).Order().ToList()
                ),
            () => Assert.NotNull(responses["200"]!["headers"]?["ETag"])
        );
    }

    private static List<string> Types(JsonNode schema) =>
        schema["type"] switch
        {
            JsonArray array => [.. array.Select(n => n!.GetValue<string>())],
            { } single => [single.GetValue<string>()],
            _ => [],
        };

    private async Task<JsonNode> PatchOperationAsync()
    {
        var document = await _client.GetFromJsonAsync<JsonObject>(
            ApiRoutes.OpenApiV1,
            CancellationToken.None
        );
        return document!["paths"]![ApiRoutes.ProductByIdTemplate]!["patch"]!;
    }

    private async Task<JsonNode> PatchBodySchemaAsync()
    {
        var document = await _client.GetFromJsonAsync<JsonObject>(
            ApiRoutes.OpenApiV1,
            CancellationToken.None
        );
        var schema = document!["paths"]![ApiRoutes.ProductByIdTemplate]!["patch"]!["requestBody"]![
            "content"
        ]![MergePatchJson]!["schema"]!;

        return schema["$ref"] is { } reference
            ? document["components"]!["schemas"]![reference.GetValue<string>().Split('/')[^1]]!
            : schema;
    }
}
