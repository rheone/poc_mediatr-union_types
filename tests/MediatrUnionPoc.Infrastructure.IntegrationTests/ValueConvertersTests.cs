using MediatrUnionPoc.Domain;
using Vogen;

namespace MediatrUnionPoc.Infrastructure.IntegrationTests;

/// <summary>
/// Exercises <see cref="ProductIdValueConverter"/> and <see cref="MoneyValueConverter"/>'s
/// conversion delegates directly. <see cref="EfCoreUnitOfWorkTests"/> and
/// <see cref="ProductRepositoryTests"/> already prove these converters work correctly when EF Core
/// drives them end-to-end, but neither pins down the delegates' own behavior in isolation.
/// </summary>
[Trait("Category", "Integration")]
public class ValueConvertersTests
{
    private const decimal DecimalValue = 9.99m;
    private static readonly Guid GuidValue = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>Verifies <see cref="UtcTicksValueConverter"/> stores an instant as the tick count of its UTC time, whatever offset it was expressed in.</summary>
    [Fact]
    public void ConvertToProvider_OffsetInstant_ReturnsUtcTicks_Test()
    {
        // Arrange
        var fivePastNoonPlusTwo = new DateTimeOffset(2026, 5, 1, 14, 5, 0, TimeSpan.FromHours(2));
        var converter = new UtcTicksValueConverter();

        // Act
        var converted = converter.ConvertToProvider(fivePastNoonPlusTwo);

        // Assert
        Assert.Equal(new DateTime(2026, 5, 1, 12, 5, 0, DateTimeKind.Utc).Ticks, converted);
    }

    /// <summary>Verifies <see cref="UtcTicksValueConverter"/> reads a tick count back as the same instant with a zero offset.</summary>
    [Fact]
    public void ConvertFromProvider_UtcTicks_ReturnsSameInstantAtZeroOffset_Test()
    {
        // Arrange
        var ticks = new DateTime(2026, 5, 1, 12, 5, 0, DateTimeKind.Utc).Ticks;
        var converter = new UtcTicksValueConverter();

        // Act
        var converted = (DateTimeOffset)converter.ConvertFromProvider(ticks)!;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(new DateTimeOffset(2026, 5, 1, 12, 5, 0, TimeSpan.Zero), converted),
            () => Assert.Equal(TimeSpan.Zero, converted.Offset)
        );
    }

    /// <summary>Verifies <see cref="ProductIdValueConverter"/> converts a <see cref="ProductId"/> to its underlying <see cref="Guid"/>.</summary>
    [Fact]
    public void ConvertToProvider_ProductId_ReturnsUnderlyingGuid_Test()
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
    public void ConvertFromProvider_Guid_ReturnsEquivalentProductId_Test()
    {
        // Arrange
        var converter = new ProductIdValueConverter();

        // Act
        var converted = converter.ConvertFromProvider(GuidValue);

        // Assert
        Assert.Equal(ProductId.From(GuidValue), converted);
    }

    /// <summary>Verifies <see cref="ProductIdValueConverter"/> rejects an empty <see cref="Guid"/> read back from storage.</summary>
    // Auto Generated, verify expected behavior: a stored empty guid fails ProductId validation on read.
    [Fact]
    public void ConvertFromProvider_EmptyGuid_ThrowsValueObjectValidationException_Test()
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
    public void ConvertToProvider_Money_ReturnsUnderlyingDecimal_Test()
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
    public void ConvertFromProvider_Decimal_ReturnsEquivalentMoney_Test()
    {
        // Arrange
        var converter = new MoneyValueConverter();

        // Act
        var converted = converter.ConvertFromProvider(DecimalValue);

        // Assert
        Assert.Equal(Money.From(DecimalValue), converted);
    }

    /// <summary>Verifies <see cref="MoneyValueConverter"/> rejects a negative <see cref="decimal"/> read back from storage.</summary>
    // Auto Generated, verify expected behavior: a stored negative amount fails Money validation on read.
    [Fact]
    public void ConvertFromProvider_NegativeDecimal_ThrowsValueObjectValidationException_Test()
    {
        // Arrange
        var converter = new MoneyValueConverter();

        // Act
        var act = () => converter.ConvertFromProvider(-1m);

        // Assert
        var exception = Assert.Throws<ValueObjectValidationException>(act);
        Assert.Contains("Money", exception.Message);
    }

    /// <summary>Verifies <see cref="ProductVersionValueConverter"/> converts a <see cref="ProductVersion"/> to its underlying <see cref="long"/>.</summary>
    [Fact]
    public void ConvertToProvider_ProductVersion_ReturnsUnderlyingLong_Test()
    {
        // Arrange
        var version = ProductVersion.From(7);
        var converter = new ProductVersionValueConverter();

        // Act
        var converted = converter.ConvertToProvider(version);

        // Assert
        Assert.Equal(7L, converted);
    }
}
