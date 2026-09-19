namespace MediatrUnionPoc.Domain.Tests;

/// <summary>
/// Verifies <see cref="ProductId"/>'s Vogen-generated <c>Validate</c> rejects an empty guid, and
/// that <see cref="ProductId.New"/> always produces a valid, non-empty id.
/// </summary>
public class ProductIdTests
{
    /// <summary>Verifies <see cref="Guid.Empty"/> fails validation via <c>ProductId.TryFrom</c>.</summary>
    [Fact]
    public void TryFrom_empty_guid_returns_false()
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
    public void From_non_empty_guid_round_trips_unchanged()
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
    public void New_produces_a_non_empty_id()
    {
        // Arrange

        // Act
        var id = ProductId.New();

        // Assert
        Assert.NotEqual(Guid.Empty, id.Value);
    }

    // Auto Generated, verify expected behavior:
    /// <summary>Verifies <see cref="ProductId.From"/> rejects an empty guid by throwing.</summary>
    [Fact]
    public void From_empty_guid_throws_validation_exception()
    {
        // Arrange
        var empty = Guid.Empty;

        // Act
        var ex = Assert.Throws<Vogen.ValueObjectValidationException>(() => ProductId.From(empty));

        // Assert
        Assert.Contains("empty guid", ex.Message);
    }

    // Auto Generated, verify expected behavior:
    /// <summary>Verifies two ids built from the same guid are equal (structural, not reference, equality).</summary>
    [Fact]
    public void Equals_same_guid_returns_true()
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
