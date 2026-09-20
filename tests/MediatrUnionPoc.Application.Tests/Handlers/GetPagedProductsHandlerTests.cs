using System.Runtime.CompilerServices;
using MediatrUnionPoc.Application.Common.Results;
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
        var first = Product.Create(FirstName, Money.From(FirstPrice), DateTimeOffset.UnixEpoch);
        var second = Product.Create(SecondName, Money.From(SecondPrice), DateTimeOffset.UnixEpoch);
        _repository
            .GetPagedAsync(
                1,
                10,
                Arg.Any<ProductCriteria>(),
                Arg.Any<IReadOnlyList<ProductSort>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                new PagedResult<Product>(
                    [first, second],
                    PageNumber: 1,
                    PageSize: 10,
                    TotalCount: 2,
                    Sort: ProductSort.Default
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
        await _repository
            .Received(1)
            .GetPagedAsync(
                1,
                10,
                Arg.Any<ProductCriteria>(),
                Arg.Any<IReadOnlyList<ProductSort>>(),
                Arg.Any<CancellationToken>()
            );
    }

    /// <summary>Verifies the handler passes the repository's paging metadata through unchanged.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_EmptyPage_PassesPagingMetadataThrough_Test()
    {
        // Arrange
        _repository
            .GetPagedAsync(
                2,
                5,
                Arg.Any<ProductCriteria>(),
                Arg.Any<IReadOnlyList<ProductSort>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                new PagedResult<Product>(
                    [],
                    PageNumber: 2,
                    PageSize: 5,
                    TotalCount: 12,
                    Sort: ProductSort.Default
                )
            );

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
        await _repository
            .Received(1)
            .GetPagedAsync(
                2,
                5,
                Arg.Any<ProductCriteria>(),
                Arg.Any<IReadOnlyList<ProductSort>>(),
                Arg.Any<CancellationToken>()
            );
    }

    /// <summary>Verifies the query's filters reach the repository as the equivalent <see cref="ProductCriteria"/>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_FiltersInQuery_PassesEquivalentCriteriaToRepository_Test()
    {
        // Arrange
        _repository
            .GetPagedAsync(
                default,
                default,
                default!,
                default!,
                TestContext.Current.CancellationToken
            )
            .ReturnsForAnyArgs(new PagedResult<Product>([], 3, 20, 0, ProductSort.Default));
        var query = new GetPagedProductsQuery(3, 20, "widget", 1.5m, 9.5m, "owner-1");

        // Act
        await _sut.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        await _repository
            .Received(1)
            .GetPagedAsync(
                3,
                20,
                new ProductCriteria("widget", 1.5m, 9.5m, "owner-1"),
                Arg.Any<IReadOnlyList<ProductSort>>(),
                Arg.Any<CancellationToken>()
            );
    }

    /// <summary>Verifies the sort text is parsed into ordered keys before it reaches the repository.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_SortText_PassesParsedKeysInOrderToRepository_Test()
    {
        // Arrange
        _repository
            .GetPagedAsync(
                default,
                default,
                default!,
                default!,
                TestContext.Current.CancellationToken
            )
            .ReturnsForAnyArgs(new PagedResult<Product>([], 1, 10, 0, ProductSort.Default));

        // Act
        await _sut.Handle(
            new GetPagedProductsQuery(1, 10, Sort: "-price,name"),
            CancellationToken.None
        );

        // Assert
        await _repository
            .Received(1)
            .GetPagedAsync(
                1,
                10,
                Arg.Any<ProductCriteria>(),
                Arg.Is<IReadOnlyList<ProductSort>>(sort =>
                    sort.SequenceEqual(
                        new[]
                        {
                            new ProductSort(ProductSortField.Price, SortDirection.Descending),
                            new ProductSort(ProductSortField.Name, SortDirection.Ascending),
                        }
                    )
                ),
                Arg.Any<CancellationToken>()
            );
    }

    /// <summary>Verifies the result carries the repository's applied sort and the derived navigation values worked out from its counts.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_MiddlePageOfMany_ReportsAppliedSortAndNavigation_Test()
    {
        // Arrange
        ProductSort[] applied = [new(ProductSortField.CreatedAt, SortDirection.Descending)];
        _repository
            .GetPagedAsync(
                default,
                default,
                default!,
                default!,
                TestContext.Current.CancellationToken
            )
            .ReturnsForAnyArgs(new PagedResult<Product>([], 2, 10, 25, applied));

        // Act
        var result = await _sut.Handle(new GetPagedProductsQuery(2, 10), CancellationToken.None);

        // Assert
        var page = Assert.IsType<PagedResult<ProductDto>>(((IUnion)result).Value);
        Assert.Multiple(
            () => Assert.Equal(applied, page.Sort),
            () => Assert.Equal(25, page.TotalCount),
            () => Assert.Equal(3, page.TotalPages),
            () => Assert.Equal(1, page.PreviousPage),
            () => Assert.Equal(3, page.NextPage)
        );
    }

    /// <summary>Verifies an unparseable sort comes back as per-field validation errors without querying the repository.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_UnparseableSort_ReturnsValidationErrorsOnSortWithoutQuerying_Test()
    {
        // Act
        var result = await _sut.Handle(
            new GetPagedProductsQuery(1, 10, Sort: "weight"),
            CancellationToken.None
        );

        // Assert
        var errors = Assert.IsType<ValidationErrors>(((IUnion)result).Value);
        Assert.Equal("Sort", Assert.Single(errors.Errors).PropertyName);
        await _repository
            .DidNotReceiveWithAnyArgs()
            .GetPagedAsync(
                default,
                default,
                default!,
                default!,
                TestContext.Current.CancellationToken
            );
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
        await _repository
            .DidNotReceiveWithAnyArgs()
            .GetPagedAsync(
                default,
                default,
                default!,
                default!,
                TestContext.Current.CancellationToken
            );
    }
}
