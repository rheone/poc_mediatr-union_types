using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>Verifies the generated OpenAPI document describes the list endpoint's query-string and paging-header contract.</summary>
[Trait("Category", "Integration")]
public sealed class ListingOpenApiTests : IDisposable
{
    private const string OpenApiDocumentUri = "/openapi/v1.json";
    private const string ListPath = "/api/products";

    private readonly ProductsApiFactory _factory = new();
    private readonly HttpClient _client;

    /// <summary>Initializes a new instance of the <see cref="ListingOpenApiTests"/> class with its own <see cref="HttpClient"/>.</summary>
    public ListingOpenApiTests() => _client = _factory.CreateClient();

    /// <inheritdoc/>
    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    /// <summary>Verifies every query parameter the list endpoint accepts is documented (names bind case-insensitively) with a description.</summary>
    /// <param name="parameter">The parameter name.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData("pageNumber")]
    [InlineData("pageSize")]
    [InlineData("nameContains")]
    [InlineData("minPrice")]
    [InlineData("maxPrice")]
    [InlineData("ownerId")]
    [InlineData("sort")]
    public async Task Document_ListOperation_DocumentsQueryParameterWithDescription_Test(
        string parameter
    )
    {
        // Arrange
        var operation = await ListOperationAsync();

        // Act
        var documented = operation["parameters"]!
            .AsArray()
            .FirstOrDefault(p =>
                string.Equals(
                    p!["name"]!.GetValue<string>(),
                    parameter,
                    StringComparison.OrdinalIgnoreCase
                )
                && p["in"]!.GetValue<string>() == "query"
            );

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(documented?["description"]?.GetValue<string>()));
    }

    /// <summary>Verifies the 200 response documents the <c>X-Total-Count</c> and <c>Link</c> headers.</summary>
    /// <param name="header">The header name.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData("X-Total-Count")]
    [InlineData("Link")]
    public async Task Document_ListOperation_SuccessResponseDeclaresPagingHeaders_Test(
        string header
    )
    {
        // Arrange
        var operation = await ListOperationAsync();

        // Act
        var declared = operation["responses"]!["200"]!["headers"]?[header];

        // Assert
        Assert.NotNull(declared);
    }

    /// <summary>Verifies the 400 response is documented as a validation problem (with per-field <c>errors</c>).</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Document_ListOperation_BadRequestIsValidationProblem_Test()
    {
        // Arrange
        var operation = await ListOperationAsync();

        // Act
        var schema = operation["responses"]!["400"]!["content"]!["application/json"]![
            "schema"
        ]!.ToJsonString();

        // Assert
        Assert.Contains("ValidationProblemDetails", schema, StringComparison.Ordinal);
    }

    /// <summary>Verifies the paged-result schema carries an example showing the metadata and the applied sort.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Document_PagedResultSchema_HasExampleWithPagingMetadata_Test()
    {
        // Arrange
        var document = await _client.GetFromJsonAsync<JsonObject>(
            OpenApiDocumentUri,
            CancellationToken.None
        );
        var schema = document!["components"]!["schemas"]!
            .AsObject()
            .First(s => s.Key.StartsWith("PagedResult", StringComparison.Ordinal))
            .Value!;

        // Act
        var example = schema["examples"]![0]!;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(3, example["totalPages"]!.GetValue<int>()),
            () => Assert.Equal("name", example["sort"]![0]!["field"]!.GetValue<string>()),
            () => Assert.NotNull(example["items"]![0]!["createdAt"])
        );
    }

    private async Task<JsonObject> ListOperationAsync()
    {
        var document = await _client.GetFromJsonAsync<JsonObject>(
            OpenApiDocumentUri,
            CancellationToken.None
        );
        return document!["paths"]![ListPath]!["get"]!.AsObject();
    }
}
