namespace MediatrUnionPoc.Api.Http;

/// <summary>Media types this API names explicitly.</summary>
public static class MediaTypes
{
    /// <summary>The JSON Merge Patch media type (RFC 7396), the only body type <c>PATCH /api/products/{id}</c> accepts.</summary>
    public const string MergePatchJson = "application/merge-patch+json";
}
