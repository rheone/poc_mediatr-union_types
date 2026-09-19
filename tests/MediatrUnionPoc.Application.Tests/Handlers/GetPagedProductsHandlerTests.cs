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

    /// <summary>Verifies the constructor rejects a null repository instead of failing on first use.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void Ctor_NullRepository_ThrowsArgumentNullException_Test()
    {
        // Act
        var ex = Assert.Throws<ArgumentNullException>(() => new GetPagedProductsHandler(null!));

        // Assert
        Assert.Equal("repository", ex.ParamName);
    }

    /// <summary>Verifies a null request is rejected with <see cref="ArgumentNullException"/> before the repository is touched.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    // Auto Generated, verify expected behavior:
    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException_Test()
    {
        // Act
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _sut.Handle(null!, CancellationToken.None)
        );

        // Assert
        Assert.Equal("request", ex.ParamName);
        await _repository.DidNotReceiveWithAnyArgs().GetPagedAsync(default, default, default);
    }
}
