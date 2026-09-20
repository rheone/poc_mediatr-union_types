namespace MediatrUnionPoc.Domain.Tests;

/// <summary>Verifies <see cref="Product.ApplyChanges"/>: a partial update that touches only the supplied fields and advances the version once.</summary>
public class ProductApplyChangesTests
{
    private const string Name = "Widget";
    private const decimal PriceValue = 9.99m;
    private const string OwnerId = "owner-1";
    private const string NewName = "  Widget Pro ";
    private const decimal NewPriceValue = 19.99m;

    private static readonly DateTimeOffset CreatedAt = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

    /// <summary>Verifies supplying only a name replaces the name (and its comparison key), leaves the price alone and advances the version exactly once.</summary>
    [Fact]
    public void ApplyChanges_NameOnly_ChangesNameKeepsPriceAndBumpsVersionOnce_Test()
    {
        // Arrange
        var product = Product.Create(Name, Money.From(PriceValue), CreatedAt, OwnerId);

        // Act
        product.ApplyChanges(name: NewName, price: null);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(NewName, product.Name),
            () => Assert.Equal("WIDGET PRO", product.NormalizedName),
            () => Assert.Equal(PriceValue, product.Price.Value),
            () => Assert.Equal(2L, product.Version.Value)
        );
    }

    /// <summary>Verifies supplying only a price replaces the price and leaves the name and its comparison key untouched.</summary>
    [Fact]
    public void ApplyChanges_PriceOnly_ChangesPriceKeepsNameAndBumpsVersionOnce_Test()
    {
        // Arrange
        var product = Product.Create(Name, Money.From(PriceValue), CreatedAt, OwnerId);

        // Act
        product.ApplyChanges(name: null, price: Money.From(NewPriceValue));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(Name, product.Name),
            () => Assert.Equal("WIDGET", product.NormalizedName),
            () => Assert.Equal(NewPriceValue, product.Price.Value),
            () => Assert.Equal(2L, product.Version.Value)
        );
    }

    /// <summary>Verifies supplying both fields changes both, still advancing the version once (not once per field).</summary>
    [Fact]
    public void ApplyChanges_NameAndPrice_ChangesBothAndBumpsVersionOnce_Test()
    {
        // Arrange
        var product = Product.Create(Name, Money.From(PriceValue), CreatedAt, OwnerId);

        // Act
        product.ApplyChanges(NewName, Money.From(NewPriceValue));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(NewName, product.Name),
            () => Assert.Equal(NewPriceValue, product.Price.Value),
            () => Assert.Equal(2L, product.Version.Value)
        );
    }

    /// <summary>Verifies a partial update never touches the creation instant, the owner or the identity.</summary>
    [Fact]
    public void ApplyChanges_AnyChange_LeavesCreatedAtOwnerAndIdUntouched_Test()
    {
        // Arrange
        var product = Product.Create(Name, Money.From(PriceValue), CreatedAt, OwnerId);
        var id = product.Id;

        // Act
        product.ApplyChanges(NewName, Money.From(NewPriceValue));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(CreatedAt, product.CreatedAt),
            () => Assert.Equal(OwnerId, product.OwnerId),
            () => Assert.Equal(id, product.Id)
        );
    }

    /// <summary>Verifies a change that supplies nothing is rejected rather than silently bumping the version.</summary>
    [Fact]
    public void ApplyChanges_NothingSupplied_ThrowsArgumentException_Test()
    {
        // Arrange
        var product = Product.Create(Name, Money.From(PriceValue), CreatedAt, OwnerId);

        // Act
        var ex = Record.Exception(() => product.ApplyChanges(name: null, price: null));

        // Assert
        Assert.Multiple(
            () => Assert.IsType<ArgumentException>(ex),
            () => Assert.Equal(1L, product.Version.Value)
        );
    }
}
