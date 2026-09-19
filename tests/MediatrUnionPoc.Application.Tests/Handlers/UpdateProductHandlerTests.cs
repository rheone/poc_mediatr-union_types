using System.Runtime.CompilerServices;
using MediatrUnionPoc.Application.Common.Authorization;
using MediatrUnionPoc.Application.Common.Results;
using MediatrUnionPoc.Application.Features.Products.Common;
using MediatrUnionPoc.Application.Features.Products.Update;
using MediatrUnionPoc.Application.Tests.TestData;
using MediatrUnionPoc.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace MediatrUnionPoc.Application.Tests.Handlers;

/// <summary>
/// Tests <see cref="UpdateProductHandler"/> directly against a substituted
/// <see cref="IProductRepository"/>, covering the Success, NotFound, and NotAuthorized union
/// cases — the latter exercised through a real <see cref="ResourceAuthorizationService"/> backed
/// by the same <see cref="OwnerAuthorizationHandler{TResource}"/> DI wiring registers, the same
/// style <c>OwnerAuthorizationHandlerTests</c> already uses for the underlying mechanism.
/// </summary>
public sealed class UpdateProductHandlerTests : IDisposable
{
    private const string OwnerId = "owner-1";
    private const string OtherUserId = "owner-2";
    private const string OriginalName = "Widget";
    private const decimal OriginalPrice = 9.99m;
    private const string NewName = "Widget Pro";
    private const decimal NewPrice = 19.99m;

    private static readonly Guid MissingProductGuid = Guid.Parse(
        "44444444-4444-4444-4444-444444444444"
    );

    private readonly IProductRepository _repository = Substitute.For<IProductRepository>();
    private readonly ServiceProvider _provider;
    private readonly UpdateProductHandler _sut;

    /// <summary>Wires up a real <see cref="IAuthorizationService"/> with the same policy/handler this feature registers in production.</summary>
    public UpdateProductHandlerTests()
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
        _sut = new UpdateProductHandler(
            _repository,
            new ResourceAuthorizationService(_provider.GetRequiredService<IAuthorizationService>())
        );
    }

    /// <inheritdoc/>
    public void Dispose() => _provider.Dispose();

    /// <summary>Verifies an existing product owned by the caller is updated and the handler returns Success.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_caller_owns_the_product_updates_it_and_returns_Success()
    {
        // Arrange
        var product = StoredProduct();

        // Act
        var result = await _sut.Handle(
            new UpdateProductCommand(
                product.Id.Value,
                NewName,
                NewPrice,
                PrincipalMother.WithId(OwnerId)
            ),
            CancellationToken.None
        );

        // Assert
        Assert.IsType<Success>(((IUnion)result).Value);
        Assert.Equal(NewName, product.Name);
        Assert.Equal(NewPrice, product.Price.Value);
    }

    /// <summary>Verifies a caller who doesn't own the product gets NotAuthorized and nothing is updated.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_caller_does_not_own_the_product_returns_NotAuthorized_and_leaves_it_unchanged()
    {
        // Arrange
        var product = StoredProduct();

        // Act
        var result = await _sut.Handle(
            new UpdateProductCommand(
                product.Id.Value,
                NewName,
                NewPrice,
                PrincipalMother.WithId(OtherUserId)
            ),
            CancellationToken.None
        );

        // Assert
        Assert.IsType<NotAuthorized>(((IUnion)result).Value);
        Assert.Equal(OriginalName, product.Name);
        Assert.Equal(OriginalPrice, product.Price.Value);
    }

    /// <summary>Verifies a missing product returns NotFound, before any ownership check runs.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task Handle_product_is_missing_returns_NotFound()
    {
        // Arrange
        // A concrete ProductId, not Arg.Any<ProductId>(): NSubstitute can't disambiguate two
        // Arg.Any<T>() matchers in the same call when T is a struct with Vogen's value equality.
        _repository
            .GetByIdAsync(ProductId.From(MissingProductGuid), Arg.Any<CancellationToken>())
            .Returns((Product?)null);

        // Act
        var result = await _sut.Handle(
            new UpdateProductCommand(
                MissingProductGuid,
                "Name",
                1m,
                PrincipalMother.WithId(OwnerId)
            ),
            CancellationToken.None
        );

        // Assert
        var notFound = Assert.IsType<NotFound<ProductId>>(((IUnion)result).Value);
        Assert.Equal(ProductId.From(MissingProductGuid), notFound.Id);
    }

    private Product StoredProduct()
    {
        var product = Product.Create(OriginalName, Money.From(OriginalPrice), ownerId: OwnerId);
        _repository.GetByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);
        return product;
    }
}
