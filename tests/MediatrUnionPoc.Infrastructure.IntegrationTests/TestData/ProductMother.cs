using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Infrastructure.IntegrationTests.TestData;

/// <summary>Object mother for the <see cref="Product"/> instances the Infrastructure tests persist.</summary>
public static class ProductMother
{
    /// <summary>The fixed identity of a product that is never added to any database.</summary>
    private static readonly Guid UnknownGuid = Guid.Parse("00000000-0000-0000-0000-00000000000a");

    /// <summary>The creation instant every mother-built product gets unless the test says otherwise.</summary>
    public static readonly DateTimeOffset DefaultCreatedAt = new(
        2026,
        1,
        2,
        3,
        4,
        5,
        TimeSpan.Zero
    );

    /// <summary>Builds the canonical product.</summary>
    /// <returns>A new <see cref="Product"/> named "Widget" priced 9.99 with owner "owner-1".</returns>
    public static Product Widget() =>
        Product.Create("Widget", Money.From(9.99m), DefaultCreatedAt, "owner-1");

    /// <summary>Builds a product with the given name and a fixed price.</summary>
    /// <param name="name">The display name.</param>
    /// <returns>A new <see cref="Product"/> priced 1.</returns>
    public static Product Named(string name) =>
        Product.Create(name, Money.From(1m), DefaultCreatedAt);

    /// <summary>Builds a product with every listing-relevant attribute chosen by the test.</summary>
    /// <param name="name">The display name.</param>
    /// <param name="price">The price.</param>
    /// <param name="createdAt">The creation instant.</param>
    /// <param name="ownerId">The owner's identifier.</param>
    /// <returns>A new <see cref="Product"/>.</returns>
    public static Product With(
        string name,
        decimal price = 1m,
        DateTimeOffset? createdAt = null,
        string ownerId = ""
    ) => Product.Create(name, Money.From(price), createdAt ?? DefaultCreatedAt, ownerId);

    /// <summary>Returns an identity that no persisted product has.</summary>
    /// <returns>A fixed, valid <see cref="ProductId"/>.</returns>
    public static ProductId UnknownId() => ProductId.From(UnknownGuid);
}
