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
    /// <summary>Verifies the handler projects each repository-returned product to a <see cref="ProductDto"/>, preserving order.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Projects_each_product_to_a_dto_in_the_repositorys_order()
    {
        var repository = Substitute.For<IProductRepository>();
        var first = Product.Create("Widget", Money.From(9.99m));
        var second = Product.Create("Gadget", Money.From(19.99m));
        repository
            .GetPagedAsync(1, 10, Arg.Any<CancellationToken>())
            .Returns(
                new PagedResult<Product>(
                    [first, second],
                    PageNumber: 1,
                    PageSize: 10,
                    TotalCount: 2
                )
            );
        var handler = new GetPagedProductsHandler(repository);

        var result = await handler.Handle(new GetPagedProductsQuery(1, 10), CancellationToken.None);

        var page = Assert.IsType<PagedResult<ProductDto>>(
            ((System.Runtime.CompilerServices.IUnion)result).Value
        );
        Assert.Collection(
            page.Items,
            dto => Assert.Equal("Widget", dto.Name),
            dto => Assert.Equal("Gadget", dto.Name)
        );
    }

    /// <summary>Verifies the handler passes the repository's paging metadata through unchanged.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Passes_paging_metadata_through_unchanged()
    {
        var repository = Substitute.For<IProductRepository>();
        repository
            .GetPagedAsync(2, 5, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Product>([], PageNumber: 2, PageSize: 5, TotalCount: 12));
        var handler = new GetPagedProductsHandler(repository);

        var result = await handler.Handle(new GetPagedProductsQuery(2, 5), CancellationToken.None);

        var page = Assert.IsType<PagedResult<ProductDto>>(
            ((System.Runtime.CompilerServices.IUnion)result).Value
        );
        Assert.Equal(2, page.PageNumber);
        Assert.Equal(5, page.PageSize);
        Assert.Equal(12, page.TotalCount);
        Assert.Empty(page.Items);
    }
}
