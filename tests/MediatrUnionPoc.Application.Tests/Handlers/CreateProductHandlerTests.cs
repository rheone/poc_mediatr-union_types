using System.Runtime.CompilerServices;
using MediatrUnionPoc.Application.Common.Results;
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

    private static readonly DateTimeOffset Now = new(2026, 6, 7, 8, 9, 10, TimeSpan.Zero);

    private readonly IProductRepository _repository = Substitute.For<IProductRepository>();
    private readonly CreateProductHandler _sut;

    /// <summary>Wires up <see cref="_sut"/> against the substituted <see cref="_repository"/>.</summary>
    public CreateProductHandlerTests() =>
        _sut = new CreateProductHandler(_repository, new FixedTimeProvider(Now));

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

    /// <summary>Verifies the constructor rejects a null repository instead of failing on first use.</summary>
    // Auto Generated, verify expected behavior:
    [Fact]
    public void Ctor_NullRepository_ThrowsArgumentNullException_Test()
    {
        // Act
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new CreateProductHandler(null!, new FixedTimeProvider(Now))
        );

        // Assert
        Assert.Equal("repository", ex.ParamName);
    }

    /// <summary>Verifies the constructor rejects a null clock instead of failing on first use.</summary>
    [Fact]
    public void Ctor_NullTimeProvider_ThrowsArgumentNullException_Test()
    {
        // Act
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new CreateProductHandler(_repository, null!)
        );

        // Assert
        Assert.Equal("timeProvider", ex.ParamName);
    }

    /// <summary>Verifies the created product, and the DTO returned for it, are stamped with the injected clock's current time.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_ValidCommand_StampsCreatedAtFromTimeProvider_Test()
    {
        // Arrange
        var command = new CreateProductCommand(ProductName, ProductPrice);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        var dto = Assert.IsType<ProductDto>(((IUnion)result).Value);
        Assert.Equal(new DateTimeOffset(2026, 6, 7, 8, 9, 10, TimeSpan.Zero), dto.CreatedAt);
        await _repository
            .Received(1)
            .AddAsync(Arg.Is<Product>(p => p.CreatedAt == Now), Arg.Any<CancellationToken>());
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
            .AddAsync(default!, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a name the repository reports as already taken yields a <see cref="Conflict"/> naming it, and nothing is added.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_NameAlreadyTaken_ReturnsConflictAndAddsNothing_Test()
    {
        // Arrange
        _repository
            .ExistsWithNameAsync(ProductName, null, Arg.Any<CancellationToken>())
            .Returns(true);
        var command = new CreateProductCommand(ProductName, ProductPrice);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        var conflict = Assert.IsType<Conflict>(((IUnion)result).Value);
        Assert.Contains($"'{ProductName}'", conflict.Message, StringComparison.Ordinal);
        await _repository
            .DidNotReceive()
            .AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>());
    }
}
