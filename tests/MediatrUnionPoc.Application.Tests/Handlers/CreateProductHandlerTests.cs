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

    private readonly IProductRepository _repository = Substitute.For<IProductRepository>();
    private readonly CreateProductHandler _sut;

    /// <summary>Wires up <see cref="_sut"/> against the substituted <see cref="_repository"/>.</summary>
    public CreateProductHandlerTests() => _sut = new CreateProductHandler(_repository);

    /// <summary>Verifies the handler adds the product to the repository and returns its DTO.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_valid_command_adds_the_product_and_returns_its_dto()
    {
        // Arrange
        var command = new CreateProductCommand(ProductName, ProductPrice);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        var dto = Assert.IsType<ProductDto>(((IUnion)result).Value);
        Assert.Equal(ProductName, dto.Name);
        Assert.Equal(ProductPrice, dto.Price);
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
    public async Task Handle_no_principal_assigns_an_empty_owner_id()
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
    public async Task Handle_principal_with_name_identifier_claim_assigns_it_as_the_owner_id()
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

    // Auto Generated, verify expected behavior:
    /// <summary>Verifies a principal with no name identifier claim yields an empty owner id rather than throwing.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_principal_without_name_identifier_claim_assigns_an_empty_owner_id()
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
