using System.Runtime.CompilerServices;
using MediatrUnionPoc.Application.Common.Results;
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
    private const string ProductName = "Widget";
    private const decimal ProductPrice = 9.99m;

    // SWEEP-AMBIGUITY: the ctor's repository and Handle(request, cancellationToken) have no ArgumentNullException
    // guards (a null request fails with a NullReferenceException) / each null reference-type parameter should throw
    // ArgumentNullException, but no such test is written because production does not do that.
    private static readonly Guid MissingProductGuid = Guid.Parse(
        "33333333-3333-3333-3333-333333333333"
    );

    private readonly IProductRepository _repository = Substitute.For<IProductRepository>();
    private readonly GetProductByIdHandler _sut;

    /// <summary>Wires up <see cref="_sut"/> against the substituted <see cref="_repository"/>.</summary>
    public GetProductByIdHandlerTests() => _sut = new GetProductByIdHandler(_repository);

    /// <summary>Verifies an existing product is returned as a <see cref="ProductDto"/>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_ProductExists_ReturnsProductDto_Test()
    {
        // Arrange
        var product = Product.Create(ProductName, Money.From(ProductPrice));
        _repository.GetByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);

        // Act
        var result = await _sut.Handle(
            new GetProductByIdQuery(product.Id.Value),
            CancellationToken.None
        );

        // Assert
        var dto = Assert.IsType<ProductDto>(((IUnion)result).Value);
        Assert.Multiple(
            () => Assert.Equal(product.Id, dto.Id),
            () => Assert.Equal(ProductName, dto.Name),
            () => Assert.Equal(ProductPrice, dto.Price)
        );
        await _repository.Received(1).GetByIdAsync(product.Id, Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies a missing product returns the NotFound union case.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_MissingProduct_ReturnsNotFound_Test()
    {
        // Arrange
        // A concrete ProductId, not Arg.Any<ProductId>(): NSubstitute can't disambiguate two
        // Arg.Any<T>() matchers in the same call when T is a struct with Vogen's value equality.
        _repository
            .GetByIdAsync(ProductId.From(MissingProductGuid), Arg.Any<CancellationToken>())
            .Returns((Product?)null);

        // Act
        var result = await _sut.Handle(
            new GetProductByIdQuery(MissingProductGuid),
            CancellationToken.None
        );

        // Assert
        var notFound = Assert.IsType<NotFound<ProductId>>(((IUnion)result).Value);
        Assert.Equal(ProductId.From(MissingProductGuid), notFound.Id);
        await _repository
            .Received(1)
            .GetByIdAsync(ProductId.From(MissingProductGuid), Arg.Any<CancellationToken>());
    }
}
