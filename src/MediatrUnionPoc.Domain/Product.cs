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

    /// <summary>
    /// The comparison key of <see cref="Name"/> (see <see cref="ProductNames.Normalize"/>), kept in
    /// step with it by this entity. Persistence puts its unique index on this value, so the
    /// database enforces exactly the duplicate rule the application checks.
    /// </summary>
    /// <value>The normalised form of the current <see cref="Name"/>.</value>
    public string NormalizedName { get; private set; }

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
    /// The optimistic-concurrency version. Starts at <see cref="ProductVersion.Initial"/> and is
    /// advanced by this entity on every mutation (never by the database); persistence treats it as
    /// a concurrency token, and the API exposes it as the product's ETag.
    /// </summary>
    /// <value>The current version; every successful mutation leaves it exactly one higher.</value>
    public ProductVersion Version { get; private set; }

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
    /// <param name="version">The concurrency version — see <see cref="Version"/>.</param>
    private Product(ProductId id, string name, Money price, string ownerId, ProductVersion version)
    {
        Id = id;
        Name = name;
        NormalizedName = ProductNames.Normalize(name);
        Price = price;
        OwnerId = ownerId;
        Version = version;
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
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="ownerId"/> is <see langword="null"/>.</exception>
    public static Product Create(string name, Money price, string ownerId = "")
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(ownerId);
        return new(ProductId.New(), name, price, ownerId, ProductVersion.Initial);
    }

    /// <summary>Replaces this product's name and price in place and advances <see cref="Version"/>. There is no partial-update overload.</summary>
    /// <param name="name">The product's new display name.</param>
    /// <param name="price">The product's new price.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public void UpdateDetails(string name, Money price)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
        NormalizedName = ProductNames.Normalize(name);
        Price = price;
        Version = Version.Next();
    }
}
