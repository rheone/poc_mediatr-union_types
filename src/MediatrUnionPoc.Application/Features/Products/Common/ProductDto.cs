using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Application.Features.Products.Common;

/// <summary>The read shape of a product returned across every "success" case in this feature's unions.</summary>
/// <param name="Id">The product's identity.</param>
/// <param name="Name">The product's display name.</param>
/// <param name="Price">The product's price.</param>
/// <param name="Version">The product's concurrency version; the API renders it as the <c>ETag</c> a client echoes back in <c>If-Match</c>.</param>
/// <param name="CreatedAt">The instant the product was created (UTC).</param>
/// <exception cref="ArgumentNullException"><paramref name="Name"/> is <see langword="null"/>.</exception>
public sealed record ProductDto(
    ProductId Id,
    string Name,
    decimal Price,
    ProductVersion Version,
    DateTimeOffset CreatedAt
)
{
    /// <summary>The product's display name; never <see langword="null"/>.</summary>
    public string Name { get; init; } = Name ?? throw new ArgumentNullException(nameof(Name));

    /// <summary>Projects a domain <see cref="Product"/> into its wire-facing DTO, unwrapping <see cref="Money"/> to a plain <see cref="decimal"/>.</summary>
    /// <param name="product">The domain entity to project.</param>
    /// <returns>The equivalent <see cref="ProductDto"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="product"/> is <see langword="null"/>.</exception>
    public static ProductDto FromDomain(Product product)
    {
        ArgumentNullException.ThrowIfNull(product);

        return new(
            product.Id,
            product.Name,
            product.Price.Value,
            product.Version,
            product.CreatedAt
        );
    }
}
