using System.Text.Json.Nodes;
using MediatrUnionPoc.Api.Contracts;
using MediatrUnionPoc.Application.Features.Products.Common;
using MediatrUnionPoc.Domain;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace MediatrUnionPoc.Api.OpenApi;

/// <summary>
/// Attaches examples to the OpenAPI schemas for this API's <c>Contracts</c> request records and for the
/// paged list response (<c>PagedResult&lt;ProductDto&gt;</c>).
/// The built-in <c>Microsoft.AspNetCore.OpenApi</c> generator has no attribute-based equivalent of
/// Swashbuckle's <c>SwaggerRequestExample</c> — a schema transformer registered via
/// <c>OpenApiOptions.AddSchemaTransformer</c> is the documented extension point instead.
/// </summary>
public sealed class ProductContractExampleTransformer : IOpenApiSchemaTransformer
{
    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="schema"/> or <paramref name="context"/> is <see langword="null"/>.</exception>
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);

        var example = context.JsonTypeInfo.Type switch
        {
            var t when t == typeof(CreateProductRequest) => new JsonObject
            {
                ["name"] = "Wireless Mouse",
                ["price"] = 24.99,
            },
            var t when t == typeof(UpdateProductRequest) => new JsonObject
            {
                ["name"] = "Wireless Mouse (v2)",
                ["price"] = 27.99,
            },
            var t when t == typeof(PagedResult<ProductDto>) => PagedProductsExample(),
            _ => null,
        };

        if (example is not null)
        {
            schema.Examples = [example];
        }

        return Task.CompletedTask;
    }

    private static JsonObject PagedProductsExample() =>
        new()
        {
            ["items"] = new JsonArray(
                new JsonObject
                {
                    ["id"] = "3f2b8a52-6c1e-4d0b-9a57-1c2d3e4f5a6b",
                    ["name"] = "Wireless Keyboard",
                    ["price"] = 39.99,
                    ["version"] = 1,
                    ["createdAt"] = "2026-03-01T09:30:00+00:00",
                },
                new JsonObject
                {
                    ["id"] = "9d8c7b6a-5e4f-4a3b-8c2d-1e0f9a8b7c6d",
                    ["name"] = "Wireless Mouse",
                    ["price"] = 24.99,
                    ["version"] = 2,
                    ["createdAt"] = "2026-03-02T14:05:00+00:00",
                }
            ),
            ["pageNumber"] = 2,
            ["pageSize"] = 2,
            ["totalCount"] = 5,
            ["sort"] = new JsonArray(
                new JsonObject { ["field"] = "name", ["direction"] = "ascending" }
            ),
            ["firstPage"] = 1,
            ["lastPage"] = 3,
            ["nextPage"] = 3,
            ["previousPage"] = 1,
            ["totalPages"] = 3,
        };
}
