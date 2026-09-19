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
    public async Task Handle_product_exists_returns_its_ProductDto()
    {
        // Arrange
        var product = Product.Create("Widget", Money.From(9.99m));
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
            () => Assert.Equal("Widget", dto.Name),
            () => Assert.Equal(9.99m, dto.Price)
        );
    }

    /// <summary>Verifies a missing product returns the NotFound union case.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_product_is_missing_returns_NotFound()
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
    }
}
