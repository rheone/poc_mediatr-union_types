using MediatrUnionPoc.Domain;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MediatrUnionPoc.Infrastructure;

/// <summary>
/// Converts <see cref="ProductId"/> to and from the raw <see cref="Guid"/> column EF Core stores.
/// </summary>
/// <remarks>
/// Vogen can generate an equivalent <c>EfCoreValueConverter</c> nested type directly on the value
/// object via <c>Conversions.EfCoreValueConverter</c>, but that would require
/// <c>MediatrUnionPoc.Domain</c> to reference EF Core just to compile the generated code — leaking
/// a persistence concern into a layer that should have none. Hand-writing the converter here
/// instead keeps Domain's only Vogen conversion as <c>Conversions.SystemTextJson</c>.
/// </remarks>
public sealed class ProductIdValueConverter()
    : ValueConverter<ProductId, Guid>(id => id.Value, value => ProductId.From(value));

/// <summary>Converts <see cref="Money"/> to and from the raw <see cref="decimal"/> column EF Core stores. See <see cref="ProductIdValueConverter"/> for why this is hand-written rather than Vogen-generated.</summary>
public sealed class MoneyValueConverter()
    : ValueConverter<Money, decimal>(money => money.Value, value => Money.From(value));

/// <summary>Converts <see cref="ProductVersion"/> to and from the raw <see cref="long"/> column EF Core stores. See <see cref="ProductIdValueConverter"/> for why this is hand-written rather than Vogen-generated.</summary>
public sealed class ProductVersionValueConverter()
    : ValueConverter<ProductVersion, long>(
        version => version.Value,
        value => ProductVersion.From(value)
    );
