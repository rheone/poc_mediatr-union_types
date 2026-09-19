namespace MediatrUnionPoc.Domain.Tests;

/// <summary>
/// Verifies <see cref="Product"/>'s two mutation entry points: <see cref="Product.Create"/> (a
/// fresh identity per call) and <see cref="Product.UpdateDetails"/> (a full replace, no partial
/// update).
/// </summary>
public class ProductTests
{
    /// <summary>Verifies <see cref="Product.Create"/> assigns the given name and price.</summary>
    [Fact]
    public void Create_assigns_the_given_name_and_price()
    {
        var product = Product.Create("Widget", Money.From(9.99m));

        Assert.Equal("Widget", product.Name);
        Assert.Equal(9.99m, product.Price.Value);
    }

    /// <summary>Verifies each call to <see cref="Product.Create"/> generates a distinct <see cref="ProductId"/>.</summary>
    [Fact]
    public void Create_generates_a_distinct_id_per_call()
    {
        var first = Product.Create("Widget", Money.From(9.99m));
        var second = Product.Create("Widget", Money.From(9.99m));

        Assert.NotEqual(first.Id, second.Id);
    }

    /// <summary>Verifies <see cref="Product.Create"/> assigns the given owner id when one is supplied.</summary>
    [Fact]
    public void Create_assigns_the_given_owner_id()
    {
        var product = Product.Create("Widget", Money.From(9.99m), "owner-1");

        Assert.Equal("owner-1", product.OwnerId);
    }

    /// <summary>Verifies <see cref="Product.Create"/> defaults the owner id to an empty string when none is supplied.</summary>
    [Fact]
    public void Create_defaults_owner_id_to_empty_string_when_not_specified()
    {
        var product = Product.Create("Widget", Money.From(9.99m));

        Assert.Equal(string.Empty, product.OwnerId);
    }

    /// <summary>Verifies <see cref="Product.UpdateDetails"/> replaces name and price, leaving the id unchanged.</summary>
    [Fact]
    public void UpdateDetails_replaces_name_and_price_but_not_id()
    {
        var product = Product.Create("Widget", Money.From(9.99m));
        var originalId = product.Id;

        product.UpdateDetails("Widget Pro", Money.From(19.99m));

        Assert.Equal(originalId, product.Id);
        Assert.Equal("Widget Pro", product.Name);
        Assert.Equal(19.99m, product.Price.Value);
    }
}
