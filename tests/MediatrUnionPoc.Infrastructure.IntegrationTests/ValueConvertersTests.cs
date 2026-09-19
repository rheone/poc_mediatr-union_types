using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Infrastructure.IntegrationTests;

/// <summary>
/// Exercises <see cref="ProductIdValueConverter"/> and <see cref="MoneyValueConverter"/>'s
/// conversion delegates directly. <see cref="InMemoryUnitOfWorkTests"/> and
/// <see cref="ProductRepositoryTests"/> already prove these converters work correctly when EF Core
/// drives them end-to-end, but neither pins down the delegates' own behavior in isolation.
/// </summary>
public class ValueConvertersTests
{
    /// <summary>Verifies <see cref="ProductIdValueConverter"/> converts a <see cref="ProductId"/> to its underlying <see cref="Guid"/>.</summary>
    [Fact]
    public void ProductIdValueConverter_converts_to_the_underlying_guid()
    {
        var id = ProductId.New();
        var converter = new ProductIdValueConverter();

        var converted = converter.ConvertToProvider(id);

        Assert.Equal(id.Value, converted);
    }

    /// <summary>Verifies <see cref="ProductIdValueConverter"/> converts a raw <see cref="Guid"/> back into the equivalent <see cref="ProductId"/>.</summary>
    [Fact]
    public void ProductIdValueConverter_converts_from_the_underlying_guid()
    {
        var guid = Guid.NewGuid();
        var converter = new ProductIdValueConverter();

        var converted = converter.ConvertFromProvider(guid);

        Assert.Equal(ProductId.From(guid), converted);
    }

    /// <summary>Verifies <see cref="MoneyValueConverter"/> converts a <see cref="Money"/> to its underlying <see cref="decimal"/>.</summary>
    [Fact]
    public void MoneyValueConverter_converts_to_the_underlying_decimal()
    {
        var money = Money.From(9.99m);
        var converter = new MoneyValueConverter();

        var converted = converter.ConvertToProvider(money);

        Assert.Equal(9.99m, converted);
    }

    /// <summary>Verifies <see cref="MoneyValueConverter"/> converts a raw <see cref="decimal"/> back into the equivalent <see cref="Money"/>.</summary>
    [Fact]
    public void MoneyValueConverter_converts_from_the_underlying_decimal()
    {
        var converter = new MoneyValueConverter();

        var converted = converter.ConvertFromProvider(9.99m);

        Assert.Equal(Money.From(9.99m), converted);
    }
}
