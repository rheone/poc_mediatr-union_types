using System.Runtime.CompilerServices;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Products.Delete;
using MediatrUnionPoc.Application.Tests.TestData;
using MediatrUnionPoc.Domain;
using NSubstitute;

namespace MediatrUnionPoc.Application.Tests.Handlers;

/// <summary>
/// Tests <see cref="DeleteProductHandler"/> directly against a substituted
/// <see cref="IProductRepository"/>, covering the Success and NotFound union cases. Who may delete
/// is decided earlier in the pipeline by <c>AuthorizationBehavior</c> (see
/// <see cref="DeleteProductCommand"/>), so this handler never sees an unauthorized caller.
/// </summary>
public sealed class DeleteProductHandlerTests
{
    private const string OwnerId = "owner-1";
    private const string ProductName = "Widget";
    private const decimal ProductPrice = 9.99m;

    private static readonly Guid MissingProductGuid = Guid.Parse(
        "22222222-2222-2222-2222-222222222222"
    );

    private readonly IProductRepository _repository = Substitute.For<IProductRepository>();
    private readonly DeleteProductHandler _sut;

    /// <summary>Wires up <see cref="_sut"/> against the substituted <see cref="_repository"/>.</summary>
    public DeleteProductHandlerTests() => _sut = new DeleteProductHandler(_repository);

    /// <summary>Verifies an existing product is removed and the handler returns Success.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_ExistingProduct_RemovesProductAndReturnsSuccess_Test()
    {
        // Arrange
        var product = StoredProduct();

        // Act
        var result = await _sut.Handle(
            new DeleteProductCommand(product.Id.Value, PrincipalMother.WithId(OwnerId)),
            CancellationToken.None
        );

        // Assert
        Assert.IsType<Success>(((IUnion)result).Value);
        _repository.Received(1).Remove(product);
        await _repository.Received(1).GetByIdAsync(product.Id, Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies a missing product returns NotFound and nothing is removed.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_MissingProduct_ReturnsNotFoundAndRemovesNothing_Test()
    {
        // Arrange
        // A concrete ProductId, not Arg.Any<ProductId>(): NSubstitute can't disambiguate two
        // Arg.Any<T>() matchers in the same call when T is a struct with Vogen's value equality.
        _repository
            .GetByIdAsync(ProductId.From(MissingProductGuid), Arg.Any<CancellationToken>())
            .Returns((Product?)null);

        // Act
        var result = await _sut.Handle(
            new DeleteProductCommand(MissingProductGuid, PrincipalMother.WithId(OwnerId)),
            CancellationToken.None
        );

        // Assert
        var notFound = Assert.IsType<NotFound<ProductId>>(((IUnion)result).Value);
        Assert.Equal(ProductId.From(MissingProductGuid), notFound.Id);
        _repository.DidNotReceive().Remove(Arg.Any<Product>());
        await _repository
            .Received(1)
            .GetByIdAsync(ProductId.From(MissingProductGuid), Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies a supplied expected version that no longer matches is refused as PreconditionFailed and nothing is removed.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_ExpectedVersionIsStale_ReturnsPreconditionFailedAndRemovesNothing_Test()
    {
        // Arrange
        var product = StoredProduct();
        product.UpdateDetails(ProductName, Money.From(ProductPrice)); // now version 2

        // Act
        var result = await _sut.Handle(
            new DeleteProductCommand(
                product.Id.Value,
                PrincipalMother.WithId(OwnerId),
                ProductVersion.From(1)
            ),
            CancellationToken.None
        );

        // Assert
        Assert.IsType<PreconditionFailed>(((IUnion)result).Value);
        _repository.DidNotReceive().Remove(Arg.Any<Product>());
    }

    /// <summary>Verifies a supplied expected version that matches lets the delete proceed.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_ExpectedVersionMatches_RemovesProductAndReturnsSuccess_Test()
    {
        // Arrange
        var product = StoredProduct();

        // Act
        var result = await _sut.Handle(
            new DeleteProductCommand(
                product.Id.Value,
                PrincipalMother.WithId(OwnerId),
                ProductVersion.From(1)
            ),
            CancellationToken.None
        );

        // Assert
        Assert.IsType<Success>(((IUnion)result).Value);
        _repository.Received(1).Remove(product);
    }

    private Product StoredProduct()
    {
        var product = Product.Create(ProductName, Money.From(ProductPrice), ownerId: OwnerId);
        _repository.GetByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);
        return product;
    }

    /// <summary>Verifies the constructor rejects a null repository instead of failing on first use.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void Ctor_NullRepository_ThrowsArgumentNullException_Test()
    {
        // Act
        var ex = Assert.Throws<ArgumentNullException>(() => new DeleteProductHandler(null!));

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
        Assert.Empty(_repository.ReceivedCalls());
    }
}
