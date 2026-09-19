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
    /// <value>A non-empty <see cref="ProductId"/> assigned once, at construction, and never reassigned.</value>
    public ProductId Id { get; private set; }

    /// <summary>This product's display name.</summary>
    /// <value>The current name, set by <see cref="Create"/> and replaceable via <see cref="UpdateDetails"/>.</value>
    public string Name { get; private set; }

    /// <summary>This product's price.</summary>
    /// <value>The current price, set by <see cref="Create"/> and replaceable via <see cref="UpdateDetails"/>.</value>
    public Money Price { get; private set; }

    /// <summary>
    /// The identifier of the caller who owns this product, checked against the caller's
    /// identity by the resource-based authorization mechanism in
    /// <c>MediatrUnionPoc.Application.Common.Authorization</c> (see that namespace's
    /// <c>IOwnedResource</c> and <c>OwnerAuthorizationHandler{TResource}</c>) — this entity does
    /// not implement <c>IOwnedResource</c> directly, since Domain must not depend on Application;
    /// callers instead adapt a loaded <see cref="Product"/> to that interface.
    /// </summary>
    /// <value>
    /// The owning caller's identifier, or <see cref="string.Empty"/> if <see cref="Create"/> was
    /// called without one. An empty value means no owner has been asserted for this product and
    /// never matches a real caller, so ownership checks against it always fail closed.
    /// </value>
    public string OwnerId { get; private set; }

    /// <summary>
    /// Private on purpose: EF Core materializes existing rows through this constructor via
    /// constructor-parameter-to-property binding (see <c>AppDbContext.OnModelCreating</c>), while
    /// application code must go through <see cref="Create"/> instead of calling
    /// <c>new Product(...)</c> directly.
    /// </summary>
    /// <param name="id">The product's identity.</param>
    /// <param name="name">The product's display name.</param>
    /// <param name="price">The product's price.</param>
    /// <param name="ownerId">The identifier of the owning caller — see <see cref="OwnerId"/>.</param>
    private Product(ProductId id, string name, Money price, string ownerId)
    {
        Id = id;
        Name = name;
        Price = price;
        OwnerId = ownerId;
    }

    /// <summary>Creates a brand-new product with a freshly generated <see cref="ProductId"/>.</summary>
    /// <param name="name">The product's display name.</param>
    /// <param name="price">The product's price.</param>
    /// <param name="ownerId">
    /// The identifier of the caller who owns this product — see <see cref="OwnerId"/>. Defaults to
    /// an empty string (no asserted owner) so callers that don't care about ownership don't need
    /// to supply one.
    /// </param>
    /// <returns>The newly created <see cref="Product"/>.</returns>
    public static Product Create(string name, Money price, string ownerId = "") =>
        new(ProductId.New(), name, price, ownerId);

    /// <summary>Replaces this product's name and price in place. There is no partial-update overload.</summary>
    /// <param name="name">The product's new display name.</param>
    /// <param name="price">The product's new price.</param>
    public void UpdateDetails(string name, Money price)
    {
        Name = name;
        Price = price;
    }
}
