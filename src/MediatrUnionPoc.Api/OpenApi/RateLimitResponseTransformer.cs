using System.Text.Json.Nodes;
using MediatrUnionPoc.Api.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;

namespace MediatrUnionPoc.Api.OpenApi;

/// <summary>
/// Declares the <c>429 Too Many Requests</c> response on every operation the rate limiter applies to,
/// with the <c>Retry-After</c> header and an RFC 7807 example. <c>ProducesResponseType</c> is not repeated
/// on each action because the limiter covers every controller action by default; an operation exempted with
/// <c>[DisableRateLimiting]</c> can never produce a 429 and is left alone.
/// </summary>
public sealed class RateLimitResponseTransformer : IOpenApiOperationTransformer
{
    private const string ProblemJson = "application/problem+json";

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="operation"/> or <paramref name="context"/> is <see langword="null"/>.</exception>
    public async Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        var exempt = context.Description.ActionDescriptor.EndpointMetadata.Any(metadata =>
            metadata is DisableRateLimitingAttribute
        );

        if (exempt)
        {
            return;
        }

        var schema = await context.GetOrCreateSchemaAsync(
            typeof(ProblemDetails),
            parameterDescription: null,
            cancellationToken
        );

        operation.Responses ??= [];
        operation.Responses["429"] = new OpenApiResponse
        {
            Description =
                "The caller exceeded its rate limit for this kind of request. Wait the number of seconds in Retry-After, then retry.",
            Headers = new Dictionary<string, IOpenApiHeader>
            {
                ["Retry-After"] = new OpenApiHeader
                {
                    Description = "Whole seconds until the caller's budget is available again.",
                    Schema = new OpenApiSchema { Type = JsonSchemaType.Integer },
                },
            },
            Content = new Dictionary<string, IOpenApiMediaType>
            {
                [ProblemJson] = new OpenApiMediaType
                {
                    Schema = schema,
                    Example = new JsonObject
                    {
                        ["type"] = "https://tools.ietf.org/html/rfc6585#section-4",
                        ["title"] = "Too Many Requests",
                        ["status"] = 429,
                        ["detail"] = "Rate limit exceeded. Retry after 42 seconds.",
                        [ResultHttpExtensions.CodeExtensionName] =
                            ResultHttpExtensions.RateLimitedCode,
                        ["traceId"] = "4bf92f3577b34da6a3ce929d0e0e4736",
                    },
                },
            },
        };
    }
}
