using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace MediatrUnionPoc.Api.OpenApi;

/// <summary>
/// Attaches an RFC 7807 example body to every 412 and 428 response, showing what a stale or
/// missing <c>If-Match</c> looks like on the wire. Registered as an operation transformer because
/// the generic <c>ProblemDetails</c> schema is shared by every error response and so cannot carry
/// a status-specific example itself.
/// </summary>
public sealed class PreconditionProblemExampleTransformer : IOpenApiOperationTransformer
{
    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="operation"/> or <paramref name="context"/> is <see langword="null"/>.</exception>
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        if (operation.Responses is null)
        {
            return Task.CompletedTask;
        }

        foreach (var (status, response) in operation.Responses)
        {
            var example = status switch
            {
                "412" => new JsonObject
                {
                    ["type"] = "https://tools.ietf.org/html/rfc7232#section-4.2",
                    ["title"] = "Precondition Failed",
                    ["status"] = 412,
                    ["detail"] =
                        "Product 'b5e0a0a1-1c0e-4f7e-9d59-0d6a1d1c9b11' is at version 3, not the expected 2.",
                },
                "428" => new JsonObject
                {
                    ["type"] = "https://tools.ietf.org/html/rfc6585#section-3",
                    ["title"] = "Precondition Required",
                    ["status"] = 428,
                    ["detail"] =
                        "This request must be conditional: send an If-Match header carrying the ETag of the version you are changing.",
                },
                _ => null,
            };

            if (example is null || response.Content is null)
            {
                continue;
            }

            foreach (var mediaType in response.Content.Values.OfType<OpenApiMediaType>())
            {
                mediaType.Example = example.DeepClone();
            }
        }

        return Task.CompletedTask;
    }
}
