using MediatrUnionPoc.Application.Features.Products.Common;
using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Application.Tests.Features;

/// <summary>Verifies <see cref="ProductDto"/> guards its reference-type inputs.</summary>
public class ProductDtoTests
{
    private const decimal SomePrice = 9.99m;

    private static readonly ProductId SomeId = ProductId.From(
        Guid.Parse("77777777-7777-7777-7777-777777777777")
    );

    /// <summary>Verifies the positional constructor rejects a null name.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void Ctor_NullName_ThrowsArgumentNullException_Test()
    {
        // Act
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new ProductDto(SomeId, null!, SomePrice)
        );

        // Assert
        Assert.Equal("Name", ex.ParamName);
    }

    /// <summary>Verifies the projection rejects a null product.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void FromDomain_NullProduct_ThrowsArgumentNullException_Test()
    {
        // Act
        var ex = Assert.Throws<ArgumentNullException>(() => ProductDto.FromDomain(null!));

        // Assert
        Assert.Equal("product", ex.ParamName);
    }
}
