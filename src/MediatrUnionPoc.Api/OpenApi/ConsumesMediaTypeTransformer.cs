using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace MediatrUnionPoc.Api.OpenApi;

/// <summary>
/// Makes the OpenAPI request body list the media types an action declares with
/// <see cref="ConsumesAttribute"/> instead of the generator's default <c>application/json</c>
/// (the built-in generator takes its media types from the registered JSON input formatter, which
/// knows nothing about the action's <c>[Consumes]</c>). Actions without the attribute are untouched.
/// </summary>
public sealed class ConsumesMediaTypeTransformer : IOpenApiOperationTransformer
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

        var consumes = context
            .Description.ActionDescriptor.EndpointMetadata.OfType<ConsumesAttribute>()
            .FirstOrDefault();

        if (consumes is null || operation.RequestBody?.Content is not { } content)
        {
            return Task.CompletedTask;
        }

        var template = content.Values.FirstOrDefault();

        if (template is null)
        {
            return Task.CompletedTask;
        }

        content.Clear();
        foreach (var mediaType in consumes.ContentTypes)
        {
            content[mediaType] = template;
        }

        return Task.CompletedTask;
    }
}
