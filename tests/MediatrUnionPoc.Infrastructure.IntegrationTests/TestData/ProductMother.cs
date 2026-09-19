using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Infrastructure.IntegrationTests.TestData;

/// <summary>Object mother for the <see cref="Product"/> instances the Infrastructure tests persist.</summary>
public static class ProductMother
{
    /// <summary>The fixed identity of a product that is never added to any database.</summary>
    private static readonly Guid UnknownGuid = Guid.Parse("00000000-0000-0000-0000-00000000000a");

    /// <summary>Builds the canonical product.</summary>
    /// <returns>A new <see cref="Product"/> named "Widget" priced 9.99 with owner "owner-1".</returns>
    public static Product Widget() => Product.Create("Widget", Money.From(9.99m), "owner-1");

    /// <summary>Builds a product with the given name and a fixed price.</summary>
    /// <param name="name">The display name.</param>
    /// <returns>A new <see cref="Product"/> priced 1.</returns>
    public static Product Named(string name) => Product.Create(name, Money.From(1m));

    /// <summary>Returns an identity that no persisted product has.</summary>
    /// <returns>A fixed, valid <see cref="ProductId"/>.</returns>
    public static ProductId UnknownId() => ProductId.From(UnknownGuid);
}
