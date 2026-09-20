using MediatrUnionPoc.Application.Common;

namespace MediatrUnionPoc.Api.Contracts;

/// <summary>Request body for <see cref="Controllers.ProductsController.CreateAsync"/>.</summary>
public sealed record CreateProductRequest(string Name, decimal Price);

/// <summary>Request body for <see cref="Controllers.ProductsController.UpdateAsync"/>.</summary>
public sealed record UpdateProductRequest(string Name, decimal Price);

/// <summary>
/// A JSON Merge Patch (RFC 7396, <c>application/merge-patch+json</c>) for a product. Each member is
/// either absent (leave that field alone) or present (replace it). A member present as <c>null</c>
/// is rejected — name and price are required, so there is nothing to clear — as is a body that names
/// neither. Members this contract does not know are ignored, as RFC 7396 leaves unrecognised members
/// to the recipient. Binding the <see cref="Optional{T}"/> members is the job of
/// <c>OptionalJsonConverterFactory</c>; every rule on their values belongs to the application's
/// validator.
/// </summary>
/// <param name="Name">The new display name, when present.</param>
/// <param name="Price">The new price, when present.</param>
public sealed record PatchProductRequest(Optional<string?> Name, Optional<decimal?> Price);

/// <summary>
/// The query string of <see cref="Controllers.ProductsController.GetPagedAsync"/>: which products to
/// list, in what order, and which page of them. Every member is optional. Query-string names bind
/// case-insensitively, so the camel-case spellings (<c>nameContains</c>, <c>minPrice</c>, ...) and the
/// member names both work; validation errors are keyed by the member name (<c>MinPrice</c>).
/// </summary>
/// <param name="PageNumber">1-based page number (default 1).</param>
/// <param name="PageSize">Items per page, 1 to 100 (default 10).</param>
/// <param name="NameContains">Only products whose name contains this text, ignoring case and surrounding whitespace.</param>
/// <param name="MinPrice">Only products priced at least this much (inclusive).</param>
/// <param name="MaxPrice">Only products priced at most this much (inclusive); not below <c>minPrice</c>.</param>
/// <param name="OwnerId">Only products owned by exactly this caller id (the <c>sub</c> claim of the token used to create the product).</param>
/// <param name="Sort">
/// Comma-separated sort keys in priority order, each a field (<c>name</c>, <c>price</c>,
/// <c>createdAt</c>) optionally prefixed with <c>-</c> for descending, e.g. <c>name,-price</c>.
/// Defaults to <c>name</c>. Ties always break by product id, so pages are stable.
/// </param>
public sealed record ListProductsRequest(
    int PageNumber = 1,
    int PageSize = 10,
    string? NameContains = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    string? OwnerId = null,
    string? Sort = null
);
