using System.Text.Json.Nodes;
using MediatrUnionPoc.Api.Http;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace MediatrUnionPoc.Api.OpenApi;

/// <summary>
/// Declares the <c>504 Gateway Timeout</c> response on every operation the request timeout applies to, with an
/// RFC 7807 example. <c>ProducesResponseType</c> is not repeated on each action because the timeout covers
/// every controller action by default; an operation exempted with <c>[DisableRequestTimeout]</c> can never
/// produce a 504 and is left alone.
/// </summary>
public sealed class RequestTimeoutResponseTransformer : IOpenApiOperationTransformer
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
            metadata is DisableRequestTimeoutAttribute
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
        operation.Responses["504"] = new OpenApiResponse
        {
            Description =
                "The request did not complete within its time limit and was cancelled. Nothing it was doing is guaranteed to have been undone; check the resource before retrying a change.",
            Content = new Dictionary<string, IOpenApiMediaType>
            {
                [ProblemJson] = new OpenApiMediaType
                {
                    Schema = schema,
                    Example = new JsonObject
                    {
                        ["type"] = "https://tools.ietf.org/html/rfc7231#section-6.6.5",
                        ["title"] = "Gateway Timeout",
                        ["status"] = 504,
                        ["detail"] = "The request did not complete in time and was cancelled.",
                        [ResultHttpExtensions.CodeExtensionName] =
                            ResultHttpExtensions.RequestTimeoutCode,
                        ["traceId"] = "4bf92f3577b34da6a3ce929d0e0e4736",
                    },
                },
            },
        };
    }
}
