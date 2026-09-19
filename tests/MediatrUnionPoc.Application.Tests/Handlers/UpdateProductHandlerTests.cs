using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Products.Update;
using MediatrUnionPoc.Domain;
using NSubstitute;

namespace MediatrUnionPoc.Application.Tests.Handlers;

/// <summary>
/// Tests <see cref="UpdateProductHandler"/> directly against a substituted
/// <see cref="IProductRepository"/>, covering the Success and NotFound union cases.
/// </summary>
public class UpdateProductHandlerTests
{
    /// <summary>Verifies an existing product is updated and the handler returns Success.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Updates_the_product_and_returns_Success_when_found()
    {
        var repository = Substitute.For<IProductRepository>();
        var product = Product.Create("Widget", Money.From(9.99m));
        repository.GetByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);
        var handler = new UpdateProductHandler(repository);

        var result = await handler.Handle(
            new UpdateProductCommand(product.Id.Value, "Widget Pro", 19.99m),
            CancellationToken.None
        );

        Assert.IsType<Success>(((System.Runtime.CompilerServices.IUnion)result).Value);
        Assert.Equal("Widget Pro", product.Name);
        Assert.Equal(19.99m, product.Price.Value);
    }

    /// <summary>Verifies a missing product returns NotFound and nothing is updated.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Returns_NotFound_and_leaves_nothing_to_update_when_missing()
    {
        var repository = Substitute.For<IProductRepository>();
        var missingId = Guid.NewGuid();

        // A concrete ProductId, not Arg.Any<ProductId>(): NSubstitute can't disambiguate two
        // Arg.Any<T>() matchers in the same call when T is a struct with Vogen's value equality.
        repository
            .GetByIdAsync(ProductId.From(missingId), Arg.Any<CancellationToken>())
            .Returns((Product?)null);
        var handler = new UpdateProductHandler(repository);

        var result = await handler.Handle(
            new UpdateProductCommand(missingId, "Name", 1m),
            CancellationToken.None
        );

        var notFound = Assert.IsType<NotFound<ProductId>>(
            ((System.Runtime.CompilerServices.IUnion)result).Value
        );
        Assert.Equal(ProductId.From(missingId), notFound.Id);
    }
}
