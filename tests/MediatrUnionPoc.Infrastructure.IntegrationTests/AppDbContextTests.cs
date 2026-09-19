using MediatrUnionPoc.Domain;
using MediatrUnionPoc.Infrastructure.IntegrationTests.TestData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace MediatrUnionPoc.Infrastructure.IntegrationTests;

/// <summary>
/// Verifies the EF Core model <see cref="AppDbContext"/> builds for <see cref="Product"/>. The
/// InMemory provider enforces neither length limits nor value converters' storage types on its own,
/// so the model metadata is the only place these configuration choices are observable.
/// </summary>
[Trait("Category", "Integration")]
public class AppDbContextTests
{
    private const int MaxLength = 200;

    private static IEntityType ProductEntity()
    {
        using var dbContext = DbContextMother.Create(
            DbContextMother.NameFor(nameof(AppDbContextTests))
        );
        return dbContext.Model.FindEntityType(typeof(Product))!;
    }

    /// <summary>Verifies <see cref="Product.Id"/> is the entity's primary key.</summary>
    // Auto Generated, verify expected behavior: Id is the primary key.
    [Fact]
    public void Model_Product_UsesIdAsPrimaryKey_Test()
    {
        // Arrange
        var entity = ProductEntity();

        // Act
        var keyProperties = entity.FindPrimaryKey()!.Properties.Select(p => p.Name);

        // Assert
        Assert.Equal([nameof(Product.Id)], keyProperties);
    }

    /// <summary>Verifies <see cref="Product.Name"/> and <see cref="Product.OwnerId"/> are required and capped at 200 characters.</summary>
    /// <param name="propertyName">The property under test.</param>
    // Auto Generated, verify expected behavior: name and owner id are capped at 200 characters and required.
    [Theory]
    [InlineData(nameof(Product.Name))]
    [InlineData(nameof(Product.OwnerId))]
    public void Model_StringProperty_IsRequiredAndLengthCapped_Test(string propertyName)
    {
        // Arrange
        var entity = ProductEntity();

        // Act
        var property = entity.FindProperty(propertyName)!;

        // Assert
        Assert.Multiple(
            () => Assert.False(property.IsNullable),
            () => Assert.Equal(MaxLength, property.GetMaxLength())
        );
    }

    /// <summary>Verifies the Vogen value objects are stored through the hand-written converters.</summary>
    // Auto Generated, verify expected behavior: value objects are stored as their primitive underlying type.
    [Fact]
    public void Model_IdAndPrice_UseValueConverters_Test()
    {
        // Arrange
        var entity = ProductEntity();

        // Act
        var idConverter = entity.FindProperty(nameof(Product.Id))!.GetValueConverter();
        var priceConverter = entity.FindProperty(nameof(Product.Price))!.GetValueConverter();

        // Assert
        Assert.Multiple(
            () => Assert.IsType<ProductIdValueConverter>(idConverter),
            () => Assert.IsType<MoneyValueConverter>(priceConverter)
        );
    }
}
