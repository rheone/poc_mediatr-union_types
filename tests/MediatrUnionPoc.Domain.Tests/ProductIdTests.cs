namespace MediatrUnionPoc.Domain.Tests;

/// <summary>
/// Verifies <see cref="ProductId"/>'s Vogen-generated <c>Validate</c> rejects an empty guid, and
/// that <see cref="ProductId.New"/> always produces a valid, non-empty id.
/// </summary>
public class ProductIdTests
{
    /// <summary>Verifies <see cref="Guid.Empty"/> fails validation via <c>ProductId.TryFrom</c>.</summary>
    [Fact]
    public void Empty_guid_fails_validation()
    {
        var succeeded = ProductId.TryFrom(Guid.Empty, out _);

        Assert.False(succeeded);
    }

    /// <summary>Verifies a non-empty guid round-trips through <see cref="ProductId.From"/> unchanged.</summary>
    [Fact]
    public void Non_empty_guid_round_trips_unchanged()
    {
        var guid = Guid.NewGuid();

        var id = ProductId.From(guid);

        Assert.Equal(guid, id.Value);
    }

    /// <summary>Verifies <see cref="ProductId.New"/> always produces a valid, non-empty id.</summary>
    [Fact]
    public void New_always_produces_a_non_empty_id()
    {
        var id = ProductId.New();

        Assert.NotEqual(Guid.Empty, id.Value);
    }
}
