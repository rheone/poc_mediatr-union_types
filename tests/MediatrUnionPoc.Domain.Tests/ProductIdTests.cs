namespace MediatrUnionPoc.Domain.Tests;

/// <summary>
/// Verifies <see cref="ProductId"/>'s Vogen-generated <c>Validate</c> rejects an empty guid, and
/// that <see cref="ProductId.New"/> always produces a valid, non-empty id.
/// </summary>
public class ProductIdTests
{
    /// <summary>Verifies <see cref="Guid.Empty"/> fails validation via <c>ProductId.TryFrom</c>.</summary>
    [Fact]
    public void TryFrom_EmptyGuid_ReturnsFalse_Test()
    {
        // Arrange
        var empty = Guid.Empty;

        // Act
        var succeeded = ProductId.TryFrom(empty, out _);

        // Assert
        Assert.False(succeeded);
    }

    /// <summary>Verifies a non-empty guid round-trips through <see cref="ProductId.From"/> unchanged.</summary>
    [Fact]
    public void From_NonEmptyGuid_RoundTripsUnchanged_Test()
    {
        // Arrange
        var guid = new Guid("11111111-2222-3333-4444-555555555555");

        // Act
        var id = ProductId.From(guid);

        // Assert
        Assert.Equal(guid, id.Value);
    }

    /// <summary>Verifies <see cref="ProductId.New"/> always produces a valid, non-empty id.</summary>
    [Fact]
    public void New_Called_ReturnsNonEmptyId_Test()
    {
        // Arrange

        // Act
        var id = ProductId.New();

        // Assert
        Assert.NotEqual(Guid.Empty, id.Value);
    }

    /// <summary>Verifies <see cref="ProductId.From"/> rejects an empty guid by throwing.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void From_EmptyGuid_ThrowsValueObjectValidationException_Test()
    {
        // Arrange
        var empty = Guid.Empty;

        // Act
        var ex = Assert.Throws<Vogen.ValueObjectValidationException>(() => ProductId.From(empty));

        // Assert
        Assert.Contains("empty guid", ex.Message);
    }

    /// <summary>Verifies two ids built from the same guid are equal (structural, not reference, equality).</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void Equals_SameGuid_ReturnsTrue_Test()
    {
        // Arrange
        var guid = new Guid("11111111-2222-3333-4444-555555555555");

        // Act
        var first = ProductId.From(guid);
        var second = ProductId.From(guid);

        // Assert
        Assert.Equal(first, second);
    }
}
