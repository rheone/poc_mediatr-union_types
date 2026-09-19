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

    /// <summary>Verifies <see cref="Product.Create"/> assigns the given name and price.</summary>
    [Fact]
    public void Create_given_name_and_price_assigns_both()
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
    public void Create_called_twice_generates_distinct_ids()
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
    public void Create_given_owner_id_assigns_it()
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
    public void Create_without_owner_id_defaults_to_empty_string()
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
    public void UpdateDetails_new_values_replace_name_and_price_but_not_id()
    {
        // Arrange
        var product = Product.Create(Name, Money.From(PriceValue));
        var originalId = product.Id;

        // Act
        product.UpdateDetails("Widget Pro", Money.From(19.99m));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(originalId, product.Id),
            () => Assert.Equal("Widget Pro", product.Name),
            () => Assert.Equal(19.99m, product.Price.Value)
        );
    }

    // Auto Generated, verify expected behavior:
    /// <summary>Verifies <see cref="Product.UpdateDetails"/> leaves the owner id untouched.</summary>
    [Fact]
    public void UpdateDetails_new_values_leave_owner_id_unchanged()
    {
        // Arrange
        var product = Product.Create(Name, Money.From(PriceValue), OwnerId);

        // Act
        product.UpdateDetails("Widget Pro", Money.From(19.99m));

        // Assert
        Assert.Equal(OwnerId, product.OwnerId);
    }

    // SWEEP-AMBIGUITY: Product.Create and UpdateDetails perform no null/empty validation of name or ownerId, so
    // null is accepted silently (nullable annotations are the only guard); it may be intended that Domain rejects
    // blank names. No ArgumentNullException tests written.
}
