using System.Text.Json.Nodes;
using MediatrUnionPoc.Api.Contracts;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace MediatrUnionPoc.Api.OpenApi;

/// <summary>
/// Attaches request-body examples to the OpenAPI schemas for this API's <c>Contracts</c> records.
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
            _ => null,
        };

        if (example is not null)
        {
            schema.Examples = [example];
        }

        return Task.CompletedTask;
    }
}
