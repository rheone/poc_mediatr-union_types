using MediatrUnionPoc.Domain;
using MediatrUnionPoc.Infrastructure.IntegrationTests.TestData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace MediatrUnionPoc.Infrastructure.IntegrationTests;

/// <summary>
/// Verifies the EF Core model <see cref="AppDbContext"/> builds for <see cref="Product"/>. The
/// model metadata is the direct place to observe these configuration choices (length limits, value
/// converters' storage types) without depending on what a given database chooses to enforce.
/// </summary>
[Trait("Category", "Integration")]
public class AppDbContextTests
{
    private const int MaxLength = 200;

    private static IEntityType ProductEntity()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        using var dbContext = new AppDbContext(options);
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

    /// <summary>Verifies <see cref="AppDbContext"/>'s constructor rejects <see langword="null"/> options (guard supplied by EF Core's <see cref="DbContext"/> base constructor).</summary>
    // Auto Generated, verify expected behavior: pins EF Core's own guard; passes without any code in AppDbContext.
    [Fact]
    public void Ctor_NullOptions_ThrowsArgumentNullException_Test()
    {
        // Arrange
        DbContextOptions<AppDbContext>? options = null;

        // Act
        var act = () => new AppDbContext(options!);

        // Assert
        var exception = Assert.Throws<ArgumentNullException>(act);
        Assert.Equal("options", exception.ParamName);
    }
}
