using System.Security.Claims;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Products.Delete;
using MediatrUnionPoc.Domain;
using NSubstitute;

namespace MediatrUnionPoc.Application.Tests.Handlers;

/// <summary>
/// Tests <see cref="DeleteProductHandler"/> directly against a substituted
/// <see cref="IProductRepository"/>, covering the Success and NotFound union cases.
/// </summary>
public class DeleteProductHandlerTests
{
    /// <summary>Verifies an existing product is staged for removal and the handler returns Success.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Removes_the_product_and_returns_Success_when_found()
    {
        var repository = Substitute.For<IProductRepository>();
        var product = Product.Create("Widget", Money.From(9.99m));
        repository.GetByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);
        var handler = new DeleteProductHandler(repository);

        var result = await handler.Handle(
            new DeleteProductCommand(product.Id.Value, new ClaimsPrincipal()),
            CancellationToken.None
        );

        Assert.IsType<Success>(((System.Runtime.CompilerServices.IUnion)result).Value);
        repository.Received(1).Remove(product);
    }

    /// <summary>Verifies a missing product returns NotFound and nothing is staged for removal.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Returns_NotFound_and_removes_nothing_when_missing()
    {
        var repository = Substitute.For<IProductRepository>();
        var missingId = Guid.NewGuid();

        // A concrete ProductId, not Arg.Any<ProductId>(): NSubstitute can't disambiguate two
        // Arg.Any<T>() matchers in the same call when T is a struct with Vogen's value equality.
        repository
            .GetByIdAsync(ProductId.From(missingId), Arg.Any<CancellationToken>())
            .Returns((Product?)null);
        var handler = new DeleteProductHandler(repository);

        var result = await handler.Handle(
            new DeleteProductCommand(missingId, new ClaimsPrincipal()),
            CancellationToken.None
        );

        var notFound = Assert.IsType<NotFound<ProductId>>(
            ((System.Runtime.CompilerServices.IUnion)result).Value
        );
        Assert.Equal(ProductId.From(missingId), notFound.Id);
        repository.DidNotReceive().Remove(Arg.Any<Product>());
    }
}
