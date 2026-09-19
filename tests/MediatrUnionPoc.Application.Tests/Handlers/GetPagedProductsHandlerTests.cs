using System.Runtime.CompilerServices;
using MediatrUnionPoc.Application.Features.Products.Common;
using MediatrUnionPoc.Application.Features.Products.GetPaged;
using MediatrUnionPoc.Domain;
using NSubstitute;

namespace MediatrUnionPoc.Application.Tests.Handlers;

/// <summary>
/// Tests <see cref="GetPagedProductsHandler"/> directly against a substituted
/// <see cref="IProductRepository"/>, covering the projection from domain <see cref="Product"/>
/// to <see cref="ProductDto"/> and the pass-through of paging metadata.
/// </summary>
public class GetPagedProductsHandlerTests
{
    private const string FirstName = "Widget";
    private const decimal FirstPrice = 9.99m;
    private const string SecondName = "Gadget";
    private const decimal SecondPrice = 19.99m;

    // SWEEP-AMBIGUITY: the ctor's repository and Handle(request, cancellationToken) have no ArgumentNullException
    // guards (a null request fails with a NullReferenceException) / each null reference-type parameter should throw
    // ArgumentNullException, but no such test is written because production does not do that.
    private readonly IProductRepository _repository = Substitute.For<IProductRepository>();
    private readonly GetPagedProductsHandler _sut;

    /// <summary>Wires up <see cref="_sut"/> against the substituted <see cref="_repository"/>.</summary>
    public GetPagedProductsHandlerTests() => _sut = new GetPagedProductsHandler(_repository);

    /// <summary>Verifies the handler projects each repository-returned product to a <see cref="ProductDto"/>, preserving order.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_ProductsReturned_ProjectsEachToDtoInRepositoryOrder_Test()
    {
        // Arrange
        var first = Product.Create(FirstName, Money.From(FirstPrice));
        var second = Product.Create(SecondName, Money.From(SecondPrice));
        _repository
            .GetPagedAsync(1, 10, Arg.Any<CancellationToken>())
            .Returns(
                new PagedResult<Product>(
                    [first, second],
                    PageNumber: 1,
                    PageSize: 10,
                    TotalCount: 2
                )
            );

        // Act
        var result = await _sut.Handle(new GetPagedProductsQuery(1, 10), CancellationToken.None);

        // Assert
        var page = Assert.IsType<PagedResult<ProductDto>>(((IUnion)result).Value);
        Assert.Collection(
            page.Items,
            dto => Assert.Equal(first.Id, dto.Id),
            dto => Assert.Equal(second.Id, dto.Id)
        );
        Assert.Multiple(
            () => Assert.Equal([FirstName, SecondName], page.Items.Select(dto => dto.Name)),
            () => Assert.Equal([FirstPrice, SecondPrice], page.Items.Select(dto => dto.Price))
        );
        await _repository.Received(1).GetPagedAsync(1, 10, Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies the handler passes the repository's paging metadata through unchanged.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_EmptyPage_PassesPagingMetadataThrough_Test()
    {
        // Arrange
        _repository
            .GetPagedAsync(2, 5, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Product>([], PageNumber: 2, PageSize: 5, TotalCount: 12));

        // Act
        var result = await _sut.Handle(new GetPagedProductsQuery(2, 5), CancellationToken.None);

        // Assert
        var page = Assert.IsType<PagedResult<ProductDto>>(((IUnion)result).Value);
        Assert.Multiple(
            () => Assert.Equal(2, page.PageNumber),
            () => Assert.Equal(5, page.PageSize),
            () => Assert.Equal(12, page.TotalCount),
            () => Assert.Empty(page.Items)
        );
        await _repository.Received(1).GetPagedAsync(2, 5, Arg.Any<CancellationToken>());
    }
}
