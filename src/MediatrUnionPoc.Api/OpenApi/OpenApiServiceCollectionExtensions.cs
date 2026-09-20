using Asp.Versioning.ApiExplorer;
using MediatrUnionPoc.Api.Http;

namespace MediatrUnionPoc.Api.OpenApi;

/// <summary>DI registration for the OpenAPI documents: one per API version, each with every contract transformer.</summary>
public static class OpenApiServiceCollectionExtensions
{
    /// <summary>
    /// Registers the OpenAPI document named <paramref name="documentName"/> (served at
    /// <c>/openapi/{documentName}.json</c>) with all of this API's transformers. The document holds the
    /// operations whose ApiExplorer group is <paramref name="documentName"/> (the versioning
    /// integration names each group after its version, e.g. <c>v1</c>) and only those reached
    /// through the versioned route template: the transitional unversioned alias
    /// (<see cref="ApiVersions.UnversionedAliasPrefix"/>) serves the same actions but is left out, so
    /// the contract lists one URL per operation. Call once per supported version.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="documentName">The document (and ApiExplorer group) name, e.g. <see cref="ApiVersions.V1DocumentName"/>.</param>
    /// <returns><paramref name="services"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="documentName"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddVersionedOpenApi(
        this IServiceCollection services,
        string documentName
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(documentName);

        return services.AddOpenApi(
            documentName,
            options =>
            {
                options.ShouldInclude = description =>
                    string.Equals(description.GroupName, documentName, StringComparison.Ordinal)
                    && description.ActionDescriptor.AttributeRouteInfo?.Template?.Contains(
                        ApiVersions.VersionedPrefix,
                        StringComparison.Ordinal
                    ) == true;
                options.CreateSchemaReferenceId = OptionalSchemaTransformer.CreateSchemaReferenceId;
                options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
                options.AddSchemaTransformer<OptionalSchemaTransformer>();
                options.AddSchemaTransformer<ProductContractExampleTransformer>();
                options.AddOperationTransformer<ConsumesMediaTypeTransformer>();
                options.AddOperationTransformer<ETagResponseHeaderTransformer>();
                options.AddOperationTransformer<PagingResponseHeaderTransformer>();
                options.AddOperationTransformer<PreconditionProblemExampleTransformer>();
                options.AddOperationTransformer<RateLimitResponseTransformer>();
            }
        );
    }
}
