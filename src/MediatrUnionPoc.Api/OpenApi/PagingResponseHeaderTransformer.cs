using MediatrUnionPoc.Api.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace MediatrUnionPoc.Api.OpenApi;

/// <summary>
/// Marks a controller action whose 200 response carries the paging headers of
/// <see cref="PagingHttpExtensions"/> (<c>X-Total-Count</c> and <c>Link</c>), so
/// <see cref="PagingResponseHeaderTransformer"/> documents them.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ReturnsPagingHeadersAttribute : Attribute;

/// <summary>
/// Documents <c>X-Total-Count</c> and <c>Link</c> on the 200 response of an action marked
/// <see cref="ReturnsPagingHeadersAttribute"/>. <c>ProducesResponseType</c> cannot describe response
/// headers, so an operation transformer is the documented extension point (as for
/// <see cref="ETagResponseHeaderTransformer"/>).
/// </summary>
public sealed class PagingResponseHeaderTransformer : IOpenApiOperationTransformer
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

        var marked =
            context.Description.ActionDescriptor is ControllerActionDescriptor descriptor
            && descriptor.MethodInfo.IsDefined(
                typeof(ReturnsPagingHeadersAttribute),
                inherit: false
            );

        if (
            !marked
            || operation.Responses is null
            || !operation.Responses.TryGetValue("200", out var response)
            || response is not OpenApiResponse concrete
        )
        {
            return Task.CompletedTask;
        }

        concrete.Headers ??= new Dictionary<string, IOpenApiHeader>();
        concrete.Headers[PagingHttpExtensions.TotalCountHeaderName] = new OpenApiHeader
        {
            Description = "The number of products matching the filters, across all pages.",
            Schema = new OpenApiSchema { Type = JsonSchemaType.Integer },
        };
        concrete.Headers["Link"] = new OpenApiHeader
        {
            Description =
                "RFC 8288 navigation links with rel first, prev, next and last. Each is this request's URL with only pageNumber changed; prev is absent on the first page and next on the last.",
            Schema = new OpenApiSchema { Type = JsonSchemaType.String },
        };

        return Task.CompletedTask;
    }
}
