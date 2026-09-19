using System.Runtime.CompilerServices;
using MediatrUnionPoc.Application.Common.Authorization;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Products.Common;
using MediatrUnionPoc.Application.Features.Products.Delete;
using MediatrUnionPoc.Application.Tests.TestData;
using MediatrUnionPoc.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace MediatrUnionPoc.Application.Tests.Handlers;

/// <summary>
/// Tests <see cref="DeleteProductHandler"/> directly against a substituted
/// <see cref="IProductRepository"/>, covering the Success, NotFound, and NotAuthorized union
/// cases — the latter exercised through a real <see cref="ResourceAuthorizationService"/> backed by
/// the same two-handler <c>ProductOwnerOrAdministrator</c> DI wiring registers
/// (<see cref="OwnerAuthorizationHandler{TResource}"/> and
/// <see cref="AdministratorResourceOverrideAuthorizationHandler{TResource}"/>), the same style
/// <c>UpdateProductHandlerTests</c> already uses for the single-handler <c>ProductOwner</c> policy.
/// </summary>
public sealed class DeleteProductHandlerTests : IDisposable
{
    private const string OwnerId = "owner-1";
    private const string OtherUserId = "owner-2";
    private const string DeleteOperation = "Delete";
    private const string AdministratorRole = "Administrator";
    private const string ProductName = "Widget";
    private const decimal ProductPrice = 9.99m;

    // SWEEP-AMBIGUITY: the ctor's repository and resourceAuthorizationService and Handle(request,
    // cancellationToken) have no ArgumentNullException guards (a null request fails with a NullReferenceException) /
    // each null reference-type parameter should throw ArgumentNullException, but no such test is written because
    // production does not do that.
    private static readonly Guid MissingProductGuid = Guid.Parse(
        "22222222-2222-2222-2222-222222222222"
    );

    private readonly IProductRepository _repository = Substitute.For<IProductRepository>();
    private readonly ServiceProvider _provider;
    private readonly DeleteProductHandler _sut;

    /// <summary>Wires up a real <see cref="IAuthorizationService"/> with the same policy/handlers this feature registers in production.</summary>
    public DeleteProductHandlerTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore(options =>
            options.AddPolicy(
                AuthorizationPolicies.ProductOwnerOrAdministrator,
                policy =>
                    policy.Requirements.Add(
                        new OperationAuthorizationRequirement { Name = DeleteOperation }
                    )
            )
        );
        services.AddSingleton<
            IAuthorizationHandler,
            OwnerAuthorizationHandler<OwnedProductResource>
        >();
        services.AddSingleton<IAuthorizationHandler>(
            _ => new AdministratorResourceOverrideAuthorizationHandler<OwnedProductResource>(
                DeleteOperation
            )
        );
        _provider = services.BuildServiceProvider();
        _sut = new DeleteProductHandler(
            _repository,
            new ResourceAuthorizationService(_provider.GetRequiredService<IAuthorizationService>())
        );
    }

    /// <inheritdoc/>
    public void Dispose() => _provider.Dispose();

    /// <summary>Verifies the product's owner can remove it and the handler returns Success.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_CallerOwnsProduct_RemovesProductAndReturnsSuccess_Test()
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

    /// <summary>Verifies an administrator can remove a product they don't own and the handler returns Success.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_CallerIsAdministratorButNotOwner_RemovesProductAndReturnsSuccess_Test()
    {
        // Arrange
        var product = StoredProduct();

        // Act
        var result = await _sut.Handle(
            new DeleteProductCommand(
                product.Id.Value,
                PrincipalMother.WithRoles(AdministratorRole)
            ),
            CancellationToken.None
        );

        // Assert
        Assert.IsType<Success>(((IUnion)result).Value);
        _repository.Received(1).Remove(product);
        await _repository.Received(1).GetByIdAsync(product.Id, Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies a caller who neither owns the product nor is an administrator gets NotAuthorized and nothing is removed.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_CallerNeitherOwnerNorAdministrator_ReturnsNotAuthorizedAndRemovesNothing_Test()
    {
        // Arrange
        var product = StoredProduct();

        // Act
        var result = await _sut.Handle(
            new DeleteProductCommand(product.Id.Value, PrincipalMother.WithId(OtherUserId)),
            CancellationToken.None
        );

        // Assert
        Assert.IsType<NotAuthorized>(((IUnion)result).Value);
        _repository.DidNotReceive().Remove(Arg.Any<Product>());
        await _repository.Received(1).GetByIdAsync(product.Id, Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies a missing product returns NotFound and nothing is removed, before any authorization check runs.</summary>
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

    private Product StoredProduct()
    {
        var product = Product.Create(ProductName, Money.From(ProductPrice), ownerId: OwnerId);
        _repository.GetByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);
        return product;
    }
}
