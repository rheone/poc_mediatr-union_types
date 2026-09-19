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
    private readonly IProductRepository _repository = Substitute.For<IProductRepository>();
    private readonly GetPagedProductsHandler _sut;

    /// <summary>Wires up <see cref="_sut"/> against the substituted <see cref="_repository"/>.</summary>
    public GetPagedProductsHandlerTests() => _sut = new GetPagedProductsHandler(_repository);

    /// <summary>Verifies the handler projects each repository-returned product to a <see cref="ProductDto"/>, preserving order.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_products_returned_projects_each_to_a_dto_in_the_repositorys_order()
    {
        // Arrange
        var first = Product.Create("Widget", Money.From(9.99m));
        var second = Product.Create("Gadget", Money.From(19.99m));
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
        Assert.Equal(["Widget", "Gadget"], page.Items.Select(dto => dto.Name));
        Assert.Equal([9.99m, 19.99m], page.Items.Select(dto => dto.Price));
    }

    /// <summary>Verifies the handler passes the repository's paging metadata through unchanged.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_empty_page_passes_paging_metadata_through_unchanged()
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
    }
}
