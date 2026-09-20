using MediatrUnionPoc.Application.Features.Products.Update;
using MediatrUnionPoc.Domain;

namespace MediatrUnionPoc.Application.Tests.Features;

/// <summary>Verifies <see cref="UpdateProductCommand"/> guards its principal.</summary>
public class UpdateProductCommandTests
{
    private const string SomeName = "Widget";
    private const decimal SomePrice = 9.99m;

    private static readonly Guid SomeId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    /// <summary>Verifies the positional constructor rejects a null principal.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void Ctor_NullPrincipal_ThrowsArgumentNullException_Test()
    {
        // Act
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new UpdateProductCommand(SomeId, SomeName, SomePrice, null!, ProductVersion.Initial)
        );

        // Assert
        Assert.Equal("Principal", ex.ParamName);
    }
}
