using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace MediatrUnionPoc.Api.OpenApi;

/// <summary>
/// Declares the JWT bearer security scheme on the OpenAPI document and requires it for every
/// operation, so documentation UIs such as Scalar offer an authorization field and send the token
/// as <c>Authorization: Bearer ...</c>. Every operation in the document needs it: the anonymous
/// health endpoints are not part of the API description.
/// </summary>
public sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    /// <summary>The name the scheme is registered under in <c>components.securitySchemes</c>.</summary>
    public const string SchemeName = "Bearer";

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> or <paramref name="context"/> is <see langword="null"/>.</exception>
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description =
                "A JWT signed with the configured key. The sub claim identifies the caller (and becomes a product's owner); a role claim of Administrator allows deletes.",
        };

        document.Security =
        [
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(SchemeName, document)] = [],
            },
        ];

        return Task.CompletedTask;
    }
}
