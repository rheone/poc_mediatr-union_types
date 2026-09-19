namespace MediatrUnionPoc.Domain.Tests;

/// <summary>
/// Verifies <see cref="Product"/>'s two mutation entry points: <see cref="Product.Create"/> (a
/// fresh identity per call) and <see cref="Product.UpdateDetails"/> (a full replace, no partial
/// update).
/// </summary>
public class ProductTests
{
    private const string Name = "Widget";
    private const decimal PriceValue = 9.99m;
    private const string OwnerId = "owner-1";
    private const string UpdatedName = "Widget Pro";
    private const decimal UpdatedPriceValue = 19.99m;

    /// <summary>Verifies <see cref="Product.Create"/> assigns the given name and price.</summary>
    [Fact]
    public void Create_NameAndPrice_AssignsBoth_Test()
    {
        // Arrange
        var price = Money.From(PriceValue);

        // Act
        var product = Product.Create(Name, price);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(Name, product.Name),
            () => Assert.Equal(PriceValue, product.Price.Value)
        );
    }

    /// <summary>Verifies each call to <see cref="Product.Create"/> generates a distinct <see cref="ProductId"/>.</summary>
    [Fact]
    public void Create_CalledTwice_GeneratesDistinctIds_Test()
    {
        // Arrange
        var price = Money.From(PriceValue);

        // Act
        var first = Product.Create(Name, price);
        var second = Product.Create(Name, price);

        // Assert
        Assert.NotEqual(first.Id, second.Id);
    }

    /// <summary>Verifies <see cref="Product.Create"/> assigns the given owner id when one is supplied.</summary>
    [Fact]
    public void Create_OwnerId_AssignsOwnerId_Test()
    {
        // Arrange
        var price = Money.From(PriceValue);

        // Act
        var product = Product.Create(Name, price, OwnerId);

        // Assert
        Assert.Equal(OwnerId, product.OwnerId);
    }

    /// <summary>Verifies <see cref="Product.Create"/> defaults the owner id to an empty string when none is supplied.</summary>
    [Fact]
    public void Create_NoOwnerId_DefaultsToEmptyString_Test()
    {
        // Arrange
        var price = Money.From(PriceValue);

        // Act
        var product = Product.Create(Name, price);

        // Assert
        Assert.Equal(string.Empty, product.OwnerId);
    }

    /// <summary>Verifies <see cref="Product.UpdateDetails"/> replaces name and price, leaving the id unchanged.</summary>
    [Fact]
    public void UpdateDetails_NewValues_ReplacesNameAndPriceButNotId_Test()
    {
        // Arrange
        var product = Product.Create(Name, Money.From(PriceValue));
        var originalId = product.Id;

        // Act
        product.UpdateDetails(UpdatedName, Money.From(UpdatedPriceValue));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(originalId, product.Id),
            () => Assert.Equal(UpdatedName, product.Name),
            () => Assert.Equal(UpdatedPriceValue, product.Price.Value)
        );
    }

    /// <summary>Verifies <see cref="Product.UpdateDetails"/> leaves the owner id untouched.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void UpdateDetails_NewValues_LeavesOwnerIdUnchanged_Test()
    {
        // Arrange
        var product = Product.Create(Name, Money.From(PriceValue), OwnerId);

        // Act
        product.UpdateDetails(UpdatedName, Money.From(UpdatedPriceValue));

        // Assert
        Assert.Equal(OwnerId, product.OwnerId);
    }

    // SWEEP-AMBIGUITY: Product.Create and UpdateDetails perform no null/empty validation of name or ownerId, so
    // null is accepted silently (nullable annotations are the only guard); it may be intended that Domain rejects
    // blank names. No ArgumentNullException tests written.
}
