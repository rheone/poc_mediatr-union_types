using System.Diagnostics;

namespace MediatrUnionPoc.Domain;

/// <summary>
/// The aggregate root for this POC. Deliberately anemic beyond identity and mutation guards —
/// the interesting behavior here lives in the Application layer's handlers and pipeline
/// behaviors, not in the entity itself.
/// </summary>
[DebuggerDisplay("{Id}: {Name} ({Price})")]
public sealed class Product
{
    /// <summary>This product's identity.</summary>
    public ProductId Id { get; private set; }

    /// <summary>This product's display name.</summary>
    public string Name { get; private set; }

    /// <summary>This product's price.</summary>
    public Money Price { get; private set; }

    /// <summary>
    /// Private on purpose: EF Core materializes existing rows through this constructor via
    /// constructor-parameter-to-property binding (see <c>AppDbContext.OnModelCreating</c>), while
    /// application code must go through <see cref="Create"/> instead of calling
    /// <c>new Product(...)</c> directly.
    /// </summary>
    private Product(ProductId id, string name, Money price)
    {
        Id = id;
        Name = name;
        Price = price;
    }

    /// <summary>Creates a brand-new product with a freshly generated <see cref="ProductId"/>.</summary>
    /// <param name="name">The product's display name.</param>
    /// <param name="price">The product's price.</param>
    /// <returns>The newly created <see cref="Product"/>.</returns>
    public static Product Create(string name, Money price) => new(ProductId.New(), name, price);

    /// <summary>Replaces this product's name and price in place. There is no partial-update overload.</summary>
    /// <param name="name">The product's new display name.</param>
    /// <param name="price">The product's new price.</param>
    public void UpdateDetails(string name, Money price)
    {
        Name = name;
        Price = price;
    }
}
