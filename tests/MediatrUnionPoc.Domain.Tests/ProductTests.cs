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

    private static readonly DateTimeOffset CreatedAt = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

    /// <summary>Verifies <see cref="Product.Create"/> assigns the given name and price.</summary>
    [Fact]
    public void Create_NameAndPrice_AssignsBoth_Test()
    {
        // Arrange
        var price = Money.From(PriceValue);

        // Act
        var product = Product.Create(Name, price, CreatedAt);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(Name, product.Name),
            () => Assert.Equal(PriceValue, product.Price.Value)
        );
    }

    /// <summary>Verifies <see cref="Product.Create"/> records the supplied creation instant and <see cref="Product.UpdateDetails"/> leaves it alone.</summary>
    [Fact]
    public void Create_SuppliedTimestamp_ExposesItAsCreatedAtAndSurvivesUpdate_Test()
    {
        // Arrange
        var product = Product.Create(Name, Money.From(PriceValue), CreatedAt);

        // Act
        var afterCreate = product.CreatedAt;
        product.UpdateDetails(UpdatedName, Money.From(UpdatedPriceValue));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero), afterCreate),
            () => Assert.Equal(afterCreate, product.CreatedAt)
        );
    }

    /// <summary>Verifies each call to <see cref="Product.Create"/> generates a distinct <see cref="ProductId"/>.</summary>
    [Fact]
    public void Create_CalledTwice_GeneratesDistinctIds_Test()
    {
        // Arrange
        var price = Money.From(PriceValue);

        // Act
        var first = Product.Create(Name, price, CreatedAt);
        var second = Product.Create(Name, price, CreatedAt);

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
        var product = Product.Create(Name, price, CreatedAt, OwnerId);

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
        var product = Product.Create(Name, price, CreatedAt);

        // Assert
        Assert.Equal(string.Empty, product.OwnerId);
    }

    /// <summary>Verifies <see cref="Product.UpdateDetails"/> replaces name and price, leaving the id unchanged.</summary>
    [Fact]
    public void UpdateDetails_NewValues_ReplacesNameAndPriceButNotId_Test()
    {
        // Arrange
        var product = Product.Create(Name, Money.From(PriceValue), CreatedAt);
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
        var product = Product.Create(Name, Money.From(PriceValue), CreatedAt, OwnerId);

        // Act
        product.UpdateDetails(UpdatedName, Money.From(UpdatedPriceValue));

        // Assert
        Assert.Equal(OwnerId, product.OwnerId);
    }

    /// <summary>Verifies <see cref="Product.Create"/> rejects a null name.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void Create_NullName_ThrowsArgumentNullException_Test()
    {
        // Arrange
        var price = Money.From(PriceValue);

        // Act
        var ex = Assert.Throws<ArgumentNullException>(() =>
            Product.Create(null!, price, CreatedAt, OwnerId)
        );

        // Assert
        Assert.Equal("name", ex.ParamName);
    }

    /// <summary>Verifies <see cref="Product.Create"/> rejects a null owner id.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void Create_NullOwnerId_ThrowsArgumentNullException_Test()
    {
        // Arrange
        var price = Money.From(PriceValue);

        // Act
        var ex = Assert.Throws<ArgumentNullException>(() =>
            Product.Create(Name, price, CreatedAt, null!)
        );

        // Assert
        Assert.Equal("ownerId", ex.ParamName);
    }

    /// <summary>Verifies <see cref="Product.UpdateDetails"/> rejects a null name and leaves the product unchanged.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void UpdateDetails_NullName_ThrowsArgumentNullException_Test()
    {
        // Arrange
        var product = Product.Create(Name, Money.From(PriceValue), CreatedAt);

        // Act
        var ex = Assert.Throws<ArgumentNullException>(() =>
            product.UpdateDetails(null!, Money.From(UpdatedPriceValue))
        );

        // Assert
        Assert.Multiple(
            () => Assert.Equal("name", ex.ParamName),
            () => Assert.Equal(Name, product.Name)
        );
    }

    /// <summary>Verifies a new product starts at <see cref="ProductVersion.Initial"/>.</summary>
    [Fact]
    public void Create_NewProduct_StartsAtVersionOne_Test()
    {
        // Arrange

        // Act
        var product = Product.Create(Name, Money.From(PriceValue), CreatedAt);

        // Assert
        Assert.Equal(1L, product.Version.Value);
    }

    /// <summary>Verifies each <see cref="Product.UpdateDetails"/> call advances the version by one.</summary>
    [Fact]
    public void UpdateDetails_CalledTwice_AdvancesVersionToThree_Test()
    {
        // Arrange
        var product = Product.Create(Name, Money.From(PriceValue), CreatedAt);

        // Act
        product.UpdateDetails(UpdatedName, Money.From(UpdatedPriceValue));
        product.UpdateDetails(Name, Money.From(PriceValue));

        // Assert
        Assert.Equal(3L, product.Version.Value);
    }

    /// <summary>Verifies a new product carries the comparison key of its name.</summary>
    [Fact]
    public void Create_PaddedMixedCaseName_HasTrimmedUpperCaseNormalizedName_Test()
    {
        // Arrange

        // Act
        var product = Product.Create("  Blue Widget ", Money.From(PriceValue), CreatedAt);

        // Assert
        Assert.Equal("BLUE WIDGET", product.NormalizedName);
    }

    /// <summary>Verifies renaming a product re-derives its comparison key.</summary>
    [Fact]
    public void UpdateDetails_NewName_RefreshesNormalizedName_Test()
    {
        // Arrange
        var product = Product.Create(Name, Money.From(PriceValue), CreatedAt);

        // Act
        product.UpdateDetails("  Red gadget", Money.From(PriceValue));

        // Assert
        Assert.Equal("RED GADGET", product.NormalizedName);
    }
}
