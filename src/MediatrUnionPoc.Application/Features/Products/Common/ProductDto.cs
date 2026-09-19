using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Application.Features.Products.Common;

/// <summary>The read shape of a product returned across every "success" case in this feature's unions.</summary>
/// <param name="Id">The product's identity.</param>
/// <param name="Name">The product's display name.</param>
/// <param name="Price">The product's price.</param>
public sealed record ProductDto(ProductId Id, string Name, decimal Price)
{
    /// <summary>Projects a domain <see cref="Product"/> into its wire-facing DTO, unwrapping <see cref="Money"/> to a plain <see cref="decimal"/>.</summary>
    /// <param name="product">The domain entity to project.</param>
    /// <returns>The equivalent <see cref="ProductDto"/>.</returns>
    public static ProductDto FromDomain(Product product) =>
        new(product.Id, product.Name, product.Price.Value);
}
