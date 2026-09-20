using System.Text.Json.Serialization.Metadata;
using MediatrUnionPoc.Application.Common;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace MediatrUnionPoc.Api.OpenApi;

/// <summary>
/// Describes <see cref="Optional{T}"/> the way it appears on the wire: not as a wrapper object with
/// its own component schema, but as the wrapped type itself, on a member that is not
/// <c>required</c> (a missing member is what "absent" means). A present <c>null</c> is rejected by
/// the API, so the wrapped type is documented as non-nullable. Wire it with
/// <see cref="CreateSchemaReferenceId"/> (which keeps <see cref="Optional{T}"/> out of
/// <c>components/schemas</c>) and <c>OpenApiOptions.AddSchemaTransformer</c>. Only wrapped
/// primitives (string, numbers, booleans, dates) are supported; that is all the contracts use.
/// </summary>
public sealed class OptionalSchemaTransformer : IOpenApiSchemaTransformer
{
    /// <summary>Gives every type its default schema id except <see cref="Optional{T}"/>, which is inlined (a <see langword="null"/> id).</summary>
    /// <param name="type">The type being described.</param>
    /// <returns>The component schema id, or <see langword="null"/> to inline the schema.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="type"/> is <see langword="null"/>.</exception>
    public static string? CreateSchemaReferenceId(JsonTypeInfo type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return IsOptional(type.Type) ? null : OpenApiOptions.CreateDefaultSchemaReferenceId(type);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="schema"/> or <paramref name="context"/> is <see langword="null"/>.</exception>
    public async Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);

        var type = context.JsonTypeInfo.Type;

        if (IsOptional(type))
        {
            var wrapped = type.GetGenericArguments()[0];
            var inner = await context.GetOrCreateSchemaAsync(
                Nullable.GetUnderlyingType(wrapped) ?? wrapped,
                parameterDescription: null,
                cancellationToken
            );

            schema.Type = inner.Type & ~JsonSchemaType.Null;
            schema.Format = inner.Format;
            schema.Properties?.Clear();
            return;
        }

        if (context.JsonTypeInfo.Kind == JsonTypeInfoKind.Object && schema.Required is { } required)
        {
            foreach (
                var property in context.JsonTypeInfo.Properties.Where(p =>
                    IsOptional(p.PropertyType)
                )
            )
            {
                required.Remove(property.Name);
            }
        }
    }

    private static bool IsOptional(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Optional<>);
}
