using MediatrUnionPoc.Application.Features.Products.Delete;

namespace MediatrUnionPoc.Application.Tests.Features;

/// <summary>Verifies <see cref="DeleteProductCommand"/> guards its principal.</summary>
public class DeleteProductCommandTests
{
    private static readonly Guid SomeId = Guid.Parse("88888888-8888-8888-8888-888888888888");

    /// <summary>Verifies the positional constructor rejects a null principal.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void Ctor_NullPrincipal_ThrowsArgumentNullException_Test()
    {
        // Act
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new DeleteProductCommand(SomeId, null!)
        );

        // Assert
        Assert.Equal("Principal", ex.ParamName);
    }
}
