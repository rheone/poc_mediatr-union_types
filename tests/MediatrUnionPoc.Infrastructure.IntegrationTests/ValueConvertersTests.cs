using MediatrUnionPoc.Domain;
using Vogen;

namespace MediatrUnionPoc.Infrastructure.IntegrationTests;

/// <summary>
/// Exercises <see cref="ProductIdValueConverter"/> and <see cref="MoneyValueConverter"/>'s
/// conversion delegates directly. <see cref="InMemoryUnitOfWorkTests"/> and
/// <see cref="ProductRepositoryTests"/> already prove these converters work correctly when EF Core
/// drives them end-to-end, but neither pins down the delegates' own behavior in isolation.
/// </summary>
[Trait("Category", "Integration")]
public class ValueConvertersTests
{
    private static readonly Guid GuidValue = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const decimal DecimalValue = 9.99m;

    /// <summary>Verifies <see cref="ProductIdValueConverter"/> converts a <see cref="ProductId"/> to its underlying <see cref="Guid"/>.</summary>
    [Fact]
    public void ProductIdValueConverter_given_a_product_id_converts_to_the_underlying_guid()
    {
        // Arrange
        var id = ProductId.From(GuidValue);
        var converter = new ProductIdValueConverter();

        // Act
        var converted = converter.ConvertToProvider(id);

        // Assert
        Assert.Equal(GuidValue, converted);
    }

    /// <summary>Verifies <see cref="ProductIdValueConverter"/> converts a raw <see cref="Guid"/> back into the equivalent <see cref="ProductId"/>.</summary>
    [Fact]
    public void ProductIdValueConverter_given_a_guid_converts_from_the_underlying_guid()
    {
        // Arrange
        var converter = new ProductIdValueConverter();

        // Act
        var converted = converter.ConvertFromProvider(GuidValue);

        // Assert
        Assert.Equal(ProductId.From(GuidValue), converted);
    }

    // Auto Generated, verify expected behavior: a stored empty guid fails ProductId validation on read.
    /// <summary>Verifies <see cref="ProductIdValueConverter"/> rejects an empty <see cref="Guid"/> read back from storage.</summary>
    [Fact]
    public void ProductIdValueConverter_given_an_empty_guid_throws_ValueObjectValidationException()
    {
        // Arrange
        var converter = new ProductIdValueConverter();

        // Act
        var act = () => converter.ConvertFromProvider(Guid.Empty);

        // Assert
        var exception = Assert.Throws<ValueObjectValidationException>(act);
        Assert.Contains("ProductId", exception.Message);
    }

    /// <summary>Verifies <see cref="MoneyValueConverter"/> converts a <see cref="Money"/> to its underlying <see cref="decimal"/>.</summary>
    [Fact]
    public void MoneyValueConverter_given_a_money_converts_to_the_underlying_decimal()
    {
        // Arrange
        var money = Money.From(DecimalValue);
        var converter = new MoneyValueConverter();

        // Act
        var converted = converter.ConvertToProvider(money);

        // Assert
        Assert.Equal(DecimalValue, converted);
    }

    /// <summary>Verifies <see cref="MoneyValueConverter"/> converts a raw <see cref="decimal"/> back into the equivalent <see cref="Money"/>.</summary>
    [Fact]
    public void MoneyValueConverter_given_a_decimal_converts_from_the_underlying_decimal()
    {
        // Arrange
        var converter = new MoneyValueConverter();

        // Act
        var converted = converter.ConvertFromProvider(DecimalValue);

        // Assert
        Assert.Equal(Money.From(DecimalValue), converted);
    }

    // Auto Generated, verify expected behavior: a stored negative amount fails Money validation on read.
    /// <summary>Verifies <see cref="MoneyValueConverter"/> rejects a negative <see cref="decimal"/> read back from storage.</summary>
    [Fact]
    public void MoneyValueConverter_given_a_negative_decimal_throws_ValueObjectValidationException()
    {
        // Arrange
        var converter = new MoneyValueConverter();

        // Act
        var act = () => converter.ConvertFromProvider(-1m);

        // Assert
        var exception = Assert.Throws<ValueObjectValidationException>(act);
        Assert.Contains("Money", exception.Message);
    }
}
