using MediatrUnionPoc.Application.Features.Products.Common;
using MediatrUnionPoc.Application.Features.Products.GetById;
using MediatrUnionPoc.Domain;
using NSubstitute;

namespace MediatrUnionPoc.Application.Tests.Handlers;

/// <summary>
/// Tests <see cref="GetProductByIdHandler"/> directly against a substituted
/// <see cref="IProductRepository"/>, covering the ProductDto and NotFound union cases.
/// </summary>
public class GetProductByIdHandlerTests
{
    /// <summary>Verifies an existing product is returned as a <see cref="ProductDto"/>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Returns_the_ProductDto_case_when_found()
    {
        var repository = Substitute.For<IProductRepository>();
        var product = Product.Create("Widget", Money.From(9.99m));
        repository.GetByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);
        var handler = new GetProductByIdHandler(repository);

        var result = await handler.Handle(
            new GetProductByIdQuery(product.Id.Value),
            CancellationToken.None
        );

        var dto = Assert.IsType<ProductDto>(((System.Runtime.CompilerServices.IUnion)result).Value);
        Assert.Equal(product.Id, dto.Id);
    }

    /// <summary>Verifies a missing product returns the NotFound union case.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Returns_the_NotFound_case_when_missing()
    {
        var repository = Substitute.For<IProductRepository>();
        var missingId = Guid.NewGuid();

        // A concrete ProductId, not Arg.Any<ProductId>(): NSubstitute can't disambiguate two
        // Arg.Any<T>() matchers in the same call when T is a struct with Vogen's value equality.
        repository
            .GetByIdAsync(ProductId.From(missingId), Arg.Any<CancellationToken>())
            .Returns((Product?)null);
        var handler = new GetProductByIdHandler(repository);

        var result = await handler.Handle(
            new GetProductByIdQuery(missingId),
            CancellationToken.None
        );

        var notFound =
            Assert.IsType<MediatrUnionPoc.Application.Common.Results.NotFound<ProductId>>(
                ((System.Runtime.CompilerServices.IUnion)result).Value
            );
        Assert.Equal(ProductId.From(missingId), notFound.Id);
    }
}
