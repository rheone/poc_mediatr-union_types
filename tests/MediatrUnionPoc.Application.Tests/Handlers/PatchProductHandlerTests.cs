using System.Runtime.CompilerServices;
using MediatrUnionPoc.Application.Common;
using MediatrUnionPoc.Application.Common.Authorization;
using MediatrUnionPoc.Application.Common.Behaviors;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Products.Common;
using MediatrUnionPoc.Application.Features.Products.Patch;
using MediatrUnionPoc.Application.Tests.TestData;
using MediatrUnionPoc.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace MediatrUnionPoc.Application.Tests.Handlers;

/// <summary>
/// Tests <see cref="PatchProductHandler"/> against a substituted <see cref="IProductRepository"/>
/// and a real <see cref="ResourceAuthorizationService"/> wired like production: which fields change,
/// which outcome each refusal produces, and that nothing changes when the handler refuses.
/// </summary>
public sealed class PatchProductHandlerTests : IDisposable
{
    private const string OwnerId = "owner-1";
    private const string OtherUserId = "owner-2";
    private const string OriginalName = "Widget";
    private const decimal OriginalPrice = 9.99m;
    private const string NewName = "Widget Pro";
    private const decimal NewPrice = 19.99m;

    private static readonly Guid MissingProductGuid = Guid.Parse(
        "55555555-5555-5555-5555-555555555555"
    );

    private static readonly DateTimeOffset CreatedAt = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

    private readonly IProductRepository _repository = Substitute.For<IProductRepository>();
    private readonly ServiceProvider _provider;
    private readonly PatchProductHandler _sut;

    /// <summary>Wires up a real <see cref="IAuthorizationService"/> with the same policy/handler this feature registers in production.</summary>
    public PatchProductHandlerTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore(options =>
            options.AddPolicy(
                AuthorizationPolicies.ProductOwner,
                policy =>
                    policy.Requirements.Add(
                        new OperationAuthorizationRequirement { Name = "Update" }
                    )
            )
        );
        services.AddSingleton<
            IAuthorizationHandler,
            OwnerAuthorizationHandler<OwnedProductResource>
        >();
        _provider = services.BuildServiceProvider();
        _sut = new PatchProductHandler(
            _repository,
            new ResourceAuthorizationService(_provider.GetRequiredService<IAuthorizationService>())
        );
    }

    /// <inheritdoc/>
    public void Dispose() => _provider.Dispose();

    /// <summary>Verifies patching only the name changes the name, leaves the price, and returns the product at its advanced version.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_NameOnly_ChangesNameAndLeavesPrice_Test()
    {
        // Arrange
        var product = StoredProduct();

        // Act
        var result = await _sut.Handle(
            Command(product, name: Optional<string?>.Of(NewName)),
            CancellationToken.None
        );

        // Assert
        var patched = Assert.IsType<ProductDto>(((IUnion)result).Value);
        Assert.Multiple(
            () => Assert.Equal(NewName, patched.Name),
            () => Assert.Equal(OriginalPrice, patched.Price),
            () => Assert.Equal(2L, patched.Version.Value),
            () => Assert.Equal(CreatedAt, patched.CreatedAt),
            () => Assert.Equal(NewName, product.Name),
            () => Assert.Equal(OriginalPrice, product.Price.Value)
        );
    }

    /// <summary>Verifies patching only the price changes the price and leaves the name and its comparison key alone.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_PriceOnly_ChangesPriceAndLeavesName_Test()
    {
        // Arrange
        var product = StoredProduct();

        // Act
        var result = await _sut.Handle(
            Command(product, price: Optional<decimal?>.Of(NewPrice)),
            CancellationToken.None
        );

        // Assert
        var patched = Assert.IsType<ProductDto>(((IUnion)result).Value);
        Assert.Multiple(
            () => Assert.Equal(OriginalName, patched.Name),
            () => Assert.Equal(NewPrice, patched.Price),
            () => Assert.Equal(2L, patched.Version.Value),
            () => Assert.Equal("WIDGET", product.NormalizedName)
        );
    }

    /// <summary>Verifies patching both fields changes both and advances the version once.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_NameAndPrice_ChangesBothAndBumpsVersionOnce_Test()
    {
        // Arrange
        var product = StoredProduct();

        // Act
        var result = await _sut.Handle(
            Command(
                product,
                name: Optional<string?>.Of(NewName),
                price: Optional<decimal?>.Of(NewPrice)
            ),
            CancellationToken.None
        );

        // Assert
        var patched = Assert.IsType<ProductDto>(((IUnion)result).Value);
        Assert.Multiple(
            () => Assert.Equal(NewName, patched.Name),
            () => Assert.Equal(NewPrice, patched.Price),
            () => Assert.Equal(2L, patched.Version.Value)
        );
    }

    /// <summary>Verifies a missing product returns NotFound.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_MissingProduct_ReturnsNotFound_Test()
    {
        // Arrange
        // A concrete ProductId, not Arg.Any<ProductId>(): NSubstitute cannot disambiguate two
        // Arg.Any<T>() matchers in the same call when T is a struct with Vogen's value equality.
        _repository
            .GetByIdAsync(ProductId.From(MissingProductGuid), Arg.Any<CancellationToken>())
            .Returns((Product?)null);
        var command = new PatchProductCommand(
            MissingProductGuid,
            Optional<string?>.Of(NewName),
            default,
            PrincipalMother.WithId(OwnerId),
            ProductVersion.Initial
        );

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        var notFound = Assert.IsType<NotFound<ProductId>>(((IUnion)result).Value);
        Assert.Equal(ProductId.From(MissingProductGuid), notFound.Id);
    }

    /// <summary>Verifies a caller who does not own the product gets NotAuthorized and nothing is changed.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_CallerDoesNotOwnProduct_ReturnsNotAuthorizedAndLeavesProductUnchanged_Test()
    {
        // Arrange
        var product = StoredProduct();

        // Act
        var result = await _sut.Handle(
            Command(product, name: Optional<string?>.Of(NewName), callerId: OtherUserId),
            CancellationToken.None
        );

        // Assert
        Assert.IsType<NotAuthorized>(((IUnion)result).Value);
        Assert.Multiple(
            () => Assert.Equal(OriginalName, product.Name),
            () => Assert.Equal(1L, product.Version.Value)
        );
    }

    /// <summary>Verifies a NotAuthorized outcome, run through the real <see cref="TransactionBehavior{TRequest,TResponse}"/>, rolls back and never commits.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_NotAuthorizedThroughTransactionBehavior_RollsBackAndDoesNotCommit_Test()
    {
        // Arrange
        var unitOfWork = UnitOfWorkMother.Committing();
        var behavior = new TransactionBehavior<PatchProductCommand, PatchProductResult>(
            unitOfWork,
            NullLogger<TransactionBehavior<PatchProductCommand, PatchProductResult>>.Instance
        );
        var product = StoredProduct();
        var command = Command(product, name: Optional<string?>.Of(NewName), callerId: OtherUserId);

        // Act
        var result = await behavior.Handle(
            command,
            _ => _sut.Handle(command, CancellationToken.None),
            CancellationToken.None
        );

        // Assert
        Assert.IsType<NotAuthorized>(((IUnion)result).Value);
        await unitOfWork.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies a successful patch, run through the real <see cref="TransactionBehavior{TRequest,TResponse}"/>, commits.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_SuccessThroughTransactionBehavior_Commits_Test()
    {
        // Arrange
        var unitOfWork = UnitOfWorkMother.Committing();
        var behavior = new TransactionBehavior<PatchProductCommand, PatchProductResult>(
            unitOfWork,
            NullLogger<TransactionBehavior<PatchProductCommand, PatchProductResult>>.Instance
        );
        var product = StoredProduct();
        var command = Command(product, name: Optional<string?>.Of(NewName));

        // Act
        var result = await behavior.Handle(
            command,
            _ => _sut.Handle(command, CancellationToken.None),
            CancellationToken.None
        );

        // Assert
        Assert.IsType<ProductDto>(((IUnion)result).Value);
        await unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies an expected version that no longer matches the stored product's is refused up front as PreconditionFailed, and nothing is changed.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_ExpectedVersionIsStale_ReturnsPreconditionFailedAndLeavesProductUnchanged_Test()
    {
        // Arrange
        var product = StoredProduct();
        product.UpdateDetails(OriginalName, Money.From(OriginalPrice)); // someone else's write: now version 2

        // Act
        var result = await _sut.Handle(
            Command(
                product,
                name: Optional<string?>.Of(NewName),
                expectedVersion: ProductVersion.From(1)
            ),
            CancellationToken.None
        );

        // Assert
        Assert.IsType<PreconditionFailed>(((IUnion)result).Value);
        Assert.Multiple(
            () => Assert.Equal(OriginalName, product.Name),
            () => Assert.Equal(2L, product.Version.Value)
        );
    }

    /// <summary>Verifies renaming onto a name another product holds is refused as a Conflict, and the product is left unchanged.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_NewNameHeldByAnotherProduct_ReturnsConflictAndLeavesProductUnchanged_Test()
    {
        // Arrange
        var product = StoredProduct();
        _repository
            .ExistsWithNameAsync(NewName, product.Id, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _sut.Handle(
            Command(product, name: Optional<string?>.Of(NewName)),
            CancellationToken.None
        );

        // Assert
        var conflict = Assert.IsType<Conflict>(((IUnion)result).Value);
        Assert.Multiple(
            () => Assert.Contains($"'{NewName}'", conflict.Message, StringComparison.Ordinal),
            () => Assert.Equal(OriginalName, product.Name),
            () => Assert.Equal(1L, product.Version.Value)
        );
    }

    /// <summary>Verifies patching a product's name to its own current name (even re-cased) is not a duplicate of itself, even when the repository would report the name as taken.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_NameNormalisesToCurrentName_DoesNotConflictWithItself_Test()
    {
        // Arrange
        var product = StoredProduct();

        // The name is taken by this very product; only a handler that skips the check says "free".
        _repository
            .ExistsWithNameAsync(
                Arg.Any<string>(),
                Arg.Any<ProductId?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(true);

        // Act
        var result = await _sut.Handle(
            Command(product, name: Optional<string?>.Of("  WIDGET ")),
            CancellationToken.None
        );

        // Assert
        var patched = Assert.IsType<ProductDto>(((IUnion)result).Value);
        Assert.Equal("  WIDGET ", patched.Name);
    }

    /// <summary>Verifies a patch that does not touch the name never asks the repository about name uniqueness.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_PriceOnly_DoesNotCheckNameUniqueness_Test()
    {
        // Arrange
        var product = StoredProduct();

        // Act
        await _sut.Handle(
            Command(product, price: Optional<decimal?>.Of(NewPrice)),
            CancellationToken.None
        );

        // Assert
        await _repository
            .DidNotReceive()
            .ExistsWithNameAsync(
                Arg.Any<string>(),
                Arg.Any<ProductId?>(),
                Arg.Any<CancellationToken>()
            );
    }

    /// <summary>Verifies the constructor rejects a null repository instead of failing on first use.</summary>
    [Fact]
    public void Ctor_NullRepository_ThrowsArgumentNullException_Test()
    {
        // Act
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new PatchProductHandler(
                null!,
                new ResourceAuthorizationService(
                    _provider.GetRequiredService<IAuthorizationService>()
                )
            )
        );

        // Assert
        Assert.Equal("repository", ex.ParamName);
    }

    /// <summary>Verifies the constructor rejects a null resource authorization service instead of failing on first use.</summary>
    [Fact]
    public void Ctor_NullResourceAuthorizationService_ThrowsArgumentNullException_Test()
    {
        // Act
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new PatchProductHandler(_repository, null!)
        );

        // Assert
        Assert.Equal("resourceAuthorizationService", ex.ParamName);
    }

    /// <summary>Verifies a null request is rejected before the repository is touched.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
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

    private static PatchProductCommand Command(
        Product product,
        Optional<string?> name = default,
        Optional<decimal?> price = default,
        string callerId = OwnerId,
        ProductVersion? expectedVersion = null
    ) =>
        new(
            product.Id.Value,
            name,
            price,
            PrincipalMother.WithId(callerId),
            expectedVersion ?? product.Version
        );

    private Product StoredProduct()
    {
        var product = Product.Create(
            OriginalName,
            Money.From(OriginalPrice),
            CreatedAt,
            ownerId: OwnerId
        );
        _repository.GetByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);
        return product;
    }
}
