using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>Verifies the OpenAPI document declares the 504 the request timeout can produce, on every operation, with an example.</summary>
[Trait("Category", "Integration")]
public sealed class RequestTimeoutOpenApiTests : IDisposable
{
    private readonly ProductsApiFactory _factory = new();

    /// <inheritdoc/>
    public void Dispose() => _factory.Dispose();

    /// <summary>Verifies every operation declares a 504 whose body is a problem with an example naming the REQUEST_TIMEOUT code.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Document_EveryOperation_Declares504WithAProblemExample_Test()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        var document = (
            await client.GetFromJsonAsync<JsonObject>(ApiRoutes.OpenApiV1, CancellationToken.None)
        )!;

        // Assert
        var operations = document["paths"]!
            .AsObject()
            .SelectMany(path =>
                path.Value!.AsObject()
                    .Select(method => (Name: $"{method.Key} {path.Key}", Operation: method.Value!))
            )
            .ToList();
        Assert.Multiple(
            () => Assert.NotEmpty(operations),
            () =>
                Assert.All(
                    operations,
                    entry =>
                    {
                        var response = entry.Operation["responses"]!["504"];
                        Assert.True(response is not null, $"{entry.Name} declares no 504.");
                        var media = response["content"]!["application/problem+json"]!;
                        Assert.Equal("REQUEST_TIMEOUT", (string?)media["example"]!["code"]);
                        Assert.Equal(504, (int?)media["example"]!["status"]);
                        Assert.NotNull(media["schema"]);
                    }
                )
        );
    }
}
