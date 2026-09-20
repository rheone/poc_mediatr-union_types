using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace MediatrUnionPoc.Api.OpenApi;

/// <summary>
/// Marks a controller action whose successful responses carry the product's version as a weak
/// <c>ETag</c> header, so <see cref="ETagResponseHeaderTransformer"/> documents that header.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ReturnsETagAttribute : Attribute;

/// <summary>
/// Documents the <c>ETag</c> response header on every 2xx response of an action marked
/// <see cref="ReturnsETagAttribute"/>. <c>ProducesResponseType</c> cannot describe response
/// headers, so an operation transformer (registered via <c>OpenApiOptions.AddOperationTransformer</c>)
/// is the documented extension point.
/// </summary>
public sealed class ETagResponseHeaderTransformer : IOpenApiOperationTransformer
{
    private const string ETagHeaderName = "ETag";

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

        var marked =
            context.Description.ActionDescriptor is ControllerActionDescriptor descriptor
            && descriptor.MethodInfo.IsDefined(typeof(ReturnsETagAttribute), inherit: false);

        if (!marked || operation.Responses is null)
        {
            return Task.CompletedTask;
        }

        foreach (var (status, response) in operation.Responses)
        {
            if (!status.StartsWith('2') || response is not OpenApiResponse concrete)
            {
                continue;
            }

            concrete.Headers ??= new Dictionary<string, IOpenApiHeader>();
            concrete.Headers[ETagHeaderName] = new OpenApiHeader
            {
                Description =
                    "Weak entity tag of the product's current version, e.g. W/\"3\". Send it back in If-Match to update or delete conditionally.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String },
            };
        }

        return Task.CompletedTask;
    }
}
