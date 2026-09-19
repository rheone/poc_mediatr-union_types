using MediatrUnionPoc.Application.Features.Products.Common;

namespace MediatrUnionPoc.Application.Tests.Features;

/// <summary>Verifies <see cref="OwnedProductResource"/> guards its reference-type inputs.</summary>
public class OwnedProductResourceTests
{
    /// <summary>Verifies the positional constructor rejects a null owner id.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void Ctor_NullOwnerId_ThrowsArgumentNullException_Test()
    {
        // Act
        var ex = Assert.Throws<ArgumentNullException>(() => new OwnedProductResource(null!));

        // Assert
        Assert.Equal("OwnerId", ex.ParamName);
    }

    /// <summary>Verifies the adapter rejects a null product.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void FromDomain_NullProduct_ThrowsArgumentNullException_Test()
    {
        // Act
        var ex = Assert.Throws<ArgumentNullException>(() => OwnedProductResource.FromDomain(null!));

        // Assert
        Assert.Equal("product", ex.ParamName);
    }
}
