using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace MediatrUnionPoc.Api.IntegrationTests;

/// <summary>Verifies the OpenAPI document declares the 429 the rate limiter can produce, on every operation, with its header and an example.</summary>
[Trait("Category", "Integration")]
public sealed class RateLimitingOpenApiTests : IDisposable
{
    private readonly ProductsApiFactory _factory = new();

    /// <inheritdoc/>
    public void Dispose() => _factory.Dispose();

    /// <summary>Verifies every operation declares a 429 whose Retry-After header is an integer and whose body is a problem with an example naming the RATE_LIMITED code.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Document_EveryOperation_Declares429WithRetryAfterAndAProblemExample_Test()
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
                        var response = entry.Operation["responses"]!["429"];
                        Assert.True(response is not null, $"{entry.Name} declares no 429.");
                        Assert.Equal(
                            "integer",
                            (string?)response["headers"]!["Retry-After"]!["schema"]!["type"]
                        );
                        var media = response["content"]!["application/problem+json"]!;
                        Assert.Equal("RATE_LIMITED", (string?)media["example"]!["code"]);
                        Assert.Equal(429, (int?)media["example"]!["status"]);
                        Assert.NotNull(media["schema"]);
                    }
                )
        );
    }
}
