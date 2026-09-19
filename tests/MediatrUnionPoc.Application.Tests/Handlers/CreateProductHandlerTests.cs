using System.Runtime.CompilerServices;
using MediatrUnionPoc.Application.Features.Products.Common;
using MediatrUnionPoc.Application.Features.Products.Create;
using MediatrUnionPoc.Application.Tests.TestData;
using MediatrUnionPoc.Domain;
using NSubstitute;

namespace MediatrUnionPoc.Application.Tests.Handlers;

/// <summary>
/// Tests <see cref="CreateProductHandler"/> directly against a substituted
/// <see cref="IProductRepository"/>.
/// </summary>
public class CreateProductHandlerTests
{
    private const string ProductName = "Widget";
    private const decimal ProductPrice = 9.99m;
    private const string OwnerId = "owner-1";

    // SWEEP-AMBIGUITY: the ctor's repository and Handle(request, cancellationToken) have no ArgumentNullException
    // guards (a null request fails with a NullReferenceException; a null repository fails on first use) / each null
    // reference-type parameter should throw ArgumentNullException, but no such test is written because production
    // does not do that.
    private readonly IProductRepository _repository = Substitute.For<IProductRepository>();
    private readonly CreateProductHandler _sut;

    /// <summary>Wires up <see cref="_sut"/> against the substituted <see cref="_repository"/>.</summary>
    public CreateProductHandlerTests() => _sut = new CreateProductHandler(_repository);

    /// <summary>Verifies the handler adds the product to the repository and returns its DTO.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_ValidCommand_AddsProductAndReturnsDto_Test()
    {
        // Arrange
        var command = new CreateProductCommand(ProductName, ProductPrice);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        var dto = Assert.IsType<ProductDto>(((IUnion)result).Value);
        Assert.Multiple(
            () => Assert.Equal(ProductName, dto.Name),
            () => Assert.Equal(ProductPrice, dto.Price)
        );
        await _repository
            .Received(1)
            .AddAsync(
                Arg.Is<Product>(p =>
                    p.Name == ProductName && p.Price.Value == ProductPrice && p.Id == dto.Id
                ),
                Arg.Any<CancellationToken>()
            );
    }

    /// <summary>Verifies a product created without a <see cref="CreateProductCommand.Principal"/> gets an empty owner id.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_NoPrincipal_AssignsEmptyOwnerId_Test()
    {
        // Arrange
        var command = new CreateProductCommand(ProductName, ProductPrice);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        await _repository
            .Received(1)
            .AddAsync(
                Arg.Is<Product>(p => p.OwnerId == string.Empty),
                Arg.Any<CancellationToken>()
            );
    }

    /// <summary>Verifies a product created with a principal carrying a name identifier claim is owned by that claim's value.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_PrincipalWithNameIdentifierClaim_AssignsClaimAsOwnerId_Test()
    {
        // Arrange
        var command = new CreateProductCommand(
            ProductName,
            ProductPrice,
            PrincipalMother.WithId(OwnerId)
        );

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        await _repository
            .Received(1)
            .AddAsync(Arg.Is<Product>(p => p.OwnerId == OwnerId), Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies a principal with no name identifier claim yields an empty owner id rather than throwing.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    // Auto Generated, verify expected behavior:
    [Fact]
    public async Task Handle_PrincipalWithoutNameIdentifierClaim_AssignsEmptyOwnerId_Test()
    {
        // Arrange
        var command = new CreateProductCommand(
            ProductName,
            ProductPrice,
            PrincipalMother.Anonymous()
        );

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        await _repository
            .Received(1)
            .AddAsync(
                Arg.Is<Product>(p => p.OwnerId == string.Empty),
                Arg.Any<CancellationToken>()
            );
    }
}
